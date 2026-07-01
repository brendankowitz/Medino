using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Medino;

/// <summary>
/// Default mediator implementation that encapsulates request/response and publishing interaction patterns
/// </summary>
public class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of Mediator
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving services</param>
    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        if (command == null) throw new ArgumentNullException(nameof(command));

        var handler = GetRequiredService<ICommandHandler<TCommand>>();
        return handler.HandleAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var requestType = request.GetType();

        // Build handler type from request type
        var responseType = requestType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();

        if (responseType == null)
        {
            throw new InvalidOperationException($"Could not find IRequest<> interface on {requestType.Name}");
        }

        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        var handler = GetRequiredService(handlerType);

        var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync));
        if (handleMethod == null)
        {
            throw new InvalidOperationException($"HandleAsync method not found on handler {handlerType.Name}");
        }

        // Resolve the pipeline behaviors registered for this request. Each behavior instance is paired
        // with the HandleAsync MethodInfo of the specific closed-generic interface it was resolved from,
        // rather than its concrete type: a single class may implement the behavior interface for more
        // than one request type, in which case resolving HandleAsync from behavior.GetType() is ambiguous
        // (AmbiguousMatchException). Resolving from the interface also lets us skip reflection entirely
        // when no behaviors are registered.
        List<(object Instance, MethodInfo HandleAsync)> ResolveBehaviors(Type closedBehaviorInterface)
        {
            var instances = GetServices(closedBehaviorInterface).ToList();
            if (instances.Count == 0)
            {
                return new List<(object, MethodInfo)>();
            }

            var handleAsync = closedBehaviorInterface.GetMethod(nameof(IPipelineBehavior<object, TResponse>.HandleAsync))
                ?? throw new InvalidOperationException($"HandleAsync method not found on {closedBehaviorInterface.Name}");
            return instances.Select(instance => (instance, handleAsync)).ToList();
        }

        // Context behaviors (for request transformation) execute first. They must be strongly typed to
        // the concrete request type: IContextPipelineBehavior<object, TResponse> is not supported because
        // PipelineContext<T> is invariant, so a PipelineContext<TRequest> can never be passed where a
        // PipelineContext<object> is expected (it would throw ArgumentException at invocation time).
        var contextBehaviors = ResolveBehaviors(typeof(IContextPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse)));

        // Regular pipeline behaviors execute after context behaviors. Both request-typed and object-based
        // (IPipelineBehavior<object, TResponse>) behaviors are supported - the latter's HandleAsync takes
        // an `object request`, so any request instance can be passed to it.
        var behaviors = ResolveBehaviors(typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse)));
        behaviors.AddRange(ResolveBehaviors(typeof(IPipelineBehavior<,>).MakeGenericType(typeof(object), typeof(TResponse))));

        if (contextBehaviors.Count > 0 || behaviors.Count > 0)
        {
            // Create context for request transformation with correct type
            var contextType = typeof(PipelineContext<>).MakeGenericType(requestType);
            var context = Activator.CreateInstance(contextType, request);
            if (context == null)
            {
                throw new InvalidOperationException($"Failed to create pipeline context for {requestType.Name}");
            }

            // Get Request property from context
            var contextRequestProperty = contextType.GetProperty(nameof(PipelineContext<object>.Request));
            if (contextRequestProperty == null)
            {
                throw new InvalidOperationException("Request property not found on PipelineContext");
            }

            async Task<TResponse> Handler()
            {
                try
                {
                    // Use the potentially transformed request from context
                    var currentRequest = contextRequestProperty.GetValue(context);
                    var result = handleMethod.Invoke(handler, [currentRequest!, cancellationToken]);
                    if (result is Task<TResponse> typedTask)
                    {
                        return await typedTask.ConfigureAwait(false);
                    }
                    throw new InvalidOperationException("Handler did not return expected task type");
                }
                catch (TargetInvocationException tie) when (tie.InnerException != null)
                {
                    // Unwrap reflection exceptions and rethrow with original stack trace
                    ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                    throw; // unreachable
                }
            }

            // Build pipeline in reverse - start with handler
            RequestHandlerDelegate<TResponse> pipeline = Handler;

            // Add regular pipeline behaviors (execute after context behaviors)
            for (var i = behaviors.Count - 1; i >= 0; i--)
            {
                var (behavior, handleAsyncMethod) = behaviors[i];
                var next = pipeline;

                pipeline = () =>
                {
                    try
                    {
                        var currentRequest = contextRequestProperty.GetValue(context);
                        var result = handleAsyncMethod.Invoke(behavior, [currentRequest!, next, cancellationToken]);
                        if (result is Task<TResponse> typedTask)
                        {
                            return typedTask;
                        }

                        throw new InvalidOperationException("Behavior did not return expected task type");
                    }
                    catch (TargetInvocationException tie) when (tie.InnerException != null)
                    {
                        // Unwrap reflection exceptions
                        ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                        throw; // unreachable
                    }
                };
            }

            // Add context behaviors (execute first, can transform request)
            for (var i = contextBehaviors.Count - 1; i >= 0; i--)
            {
                var (contextBehavior, handleAsyncMethod) = contextBehaviors[i];
                var next = pipeline;

                pipeline = () =>
                {
                    try
                    {
                        var result = handleAsyncMethod.Invoke(contextBehavior, [context, next, cancellationToken]);
                        if (result is Task<TResponse> typedTask)
                        {
                            return typedTask;
                        }
                        throw new InvalidOperationException("Context behavior did not return expected task type");
                    }
                    catch (TargetInvocationException tie) when (tie.InnerException != null)
                    {
                        // Unwrap reflection exceptions
                        ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                        throw; // unreachable
                    }
                };
            }

            try
            {
                return await pipeline().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Try exception handlers
                var handledState = await TryHandleException<TResponse>(request, ex, cancellationToken);
                if (handledState.Handled && handledState.Response != null)
                {
                    return handledState.Response;
                }

                // Execute exception actions (for logging/translation, etc)
                var exceptionToThrow = await ExecuteExceptionActions(request, ex, cancellationToken);
                ExceptionDispatchInfo.Capture(exceptionToThrow).Throw();
                throw; // unreachable, but satisfies compiler
            }
        }
        else
        {
            try
            {
                var result = handleMethod.Invoke(handler, [request, cancellationToken]);
                if (result is Task<TResponse> typedTask)
                {
                    return await typedTask.ConfigureAwait(false);
                }
                throw new InvalidOperationException("Handler did not return expected task type");
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                // Unwrap reflection exceptions
                var ex = tie.InnerException;

                // Try exception handlers
                var handledState = await TryHandleException<TResponse>(request, ex, cancellationToken);
                if (handledState.Handled && handledState.Response != null)
                {
                    return handledState.Response;
                }

                // Execute exception actions (for logging/translation, etc)
                var exceptionToThrow = await ExecuteExceptionActions(request, ex, cancellationToken);
                ExceptionDispatchInfo.Capture(exceptionToThrow).Throw();
                throw; // unreachable, but satisfies compiler
            }
            catch (Exception ex)
            {
                // Try exception handlers
                var handledState = await TryHandleException<TResponse>(request, ex, cancellationToken);
                if (handledState.Handled && handledState.Response != null)
                {
                    return handledState.Response;
                }

                // Execute exception actions (for logging/translation, etc)
                var exceptionToThrow = await ExecuteExceptionActions(request, ex, cancellationToken);
                ExceptionDispatchInfo.Capture(exceptionToThrow).Throw();
                throw; // unreachable, but satisfies compiler
            }
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification == null) throw new ArgumentNullException(nameof(notification));

        var handlers = GetServices<INotificationHandler<TNotification>>().ToList();

        if (handlers.Count == 0)
        {
            // No handlers is OK for notifications (fire and forget)
            return;
        }

        var tasks = handlers.Select(handler => handler.HandleAsync(notification, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task<RequestExceptionHandlerState<TResponse>> TryHandleException<TResponse>(
        object request,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var state = new RequestExceptionHandlerState<TResponse>();
        var exceptionType = exception.GetType();
        var requestType = request.GetType();

        // Find all matching exception handlers
        var handlerInterfaceType = typeof(IRequestExceptionHandler<,,>).MakeGenericType(requestType, typeof(TResponse), exceptionType);
        var handlers = GetServices(handlerInterfaceType).ToList();

        if (handlers.Count > 0)
        {
            foreach (var handler in handlers)
            {
                var handleMethod = handlerInterfaceType.GetMethod(nameof(IRequestExceptionHandler<object, TResponse, Exception>.HandleAsync));
                if (handleMethod != null)
                {
                    var task = handleMethod.Invoke(handler, new[] { request, exception, state, cancellationToken });
                    if (task is Task asyncTask)
                    {
                        await asyncTask.ConfigureAwait(false);
                    }

                    if (state.Handled)
                    {
                        break;
                    }
                }
            }
        }

        return state;
    }

    private async Task<Exception> ExecuteExceptionActions(object request, Exception exception, CancellationToken cancellationToken)
    {
        var exceptionType = exception.GetType();
        var requestType = request.GetType();

        // Find all matching exception actions
        var actionInterfaceType = typeof(IRequestExceptionAction<,>).MakeGenericType(requestType, exceptionType);
        var actions = GetServices(actionInterfaceType).ToList();

        if (actions.Count > 0)
        {
            foreach (var action in actions)
            {
                var executeMethod = actionInterfaceType.GetMethod(nameof(IRequestExceptionAction<object, Exception>.ExecuteAsync));
                if (executeMethod != null)
                {
                    try
                    {
                        var task = executeMethod.Invoke(action, new[] { request, exception, cancellationToken });
                        if (task is Task asyncTask)
                        {
                            await asyncTask.ConfigureAwait(false);
                        }
                    }
                    catch (TargetInvocationException tie) when (tie.InnerException != null)
                    {
                        // Exception action threw a different exception - use it as the translated exception
                        return tie.InnerException;
                    }
                    catch (Exception ex)
                    {
                        // Exception action threw a different exception - use it as the translated exception
                        return ex;
                    }
                }
            }
        }

        // Return original exception if no translation occurred
        return exception;
    }

    private object GetRequiredService(Type type)
    {
        object? service = _serviceProvider.GetService(type);
        if (service == null)
        {
            throw new InvalidOperationException($"No service of type {type.FullName} was registered.");
        }

        return service;
    }

    private T GetRequiredService<T>()
    {
        return (T)GetRequiredService(typeof(T));
    }

    private IEnumerable<object> GetServices(Type type)
    {
        return (IEnumerable<object>)GetRequiredService(typeof(IEnumerable<>).MakeGenericType(type));
    }

    private IEnumerable<T> GetServices<T>()
    {
        return GetRequiredService<IEnumerable<T>>();
    }
}