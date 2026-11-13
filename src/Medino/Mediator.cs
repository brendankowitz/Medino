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
        // (AmbiguousMatchException). Resolving the method once from the interface (guarded by the count
        // check) also avoids the per-instance HandleAsync lookup when no behaviors are registered.
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
        // PipelineContext<object> is expected. AddMedino rejects such registrations up front; only the
        // exact-request-type interface is resolved here.
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

        // Dispatch on the runtime type so a notification published through a base or interface
        // reference still reaches its concrete handlers, mirroring SendAsync's request.GetType().
        var notificationType = notification.GetType();
        var handlerInterfaceType = typeof(INotificationHandler<>).MakeGenericType(notificationType);
        var handlers = GetServices(handlerInterfaceType).ToList();

        if (handlers.Count == 0)
        {
            // No handlers is OK for notifications (fire and forget)
            return;
        }

        var handleMethod = handlerInterfaceType.GetMethod(nameof(INotificationHandler<INotification>.HandleAsync))
            ?? throw new InvalidOperationException($"HandleAsync method not found on {handlerInterfaceType.Name}");

        // Start every handler before awaiting. Invoking eagerly - rather than lazily inside Task.WhenAll -
        // guarantees a handler that throws synchronously cannot prevent later handlers from running, and
        // lets us observe every failure instead of only the first one Task.WhenAll would surface.
        var tasks = new List<Task>(handlers.Count);
        List<Exception>? failures = null;

        foreach (var handler in handlers)
        {
            try
            {
                if (handleMethod.Invoke(handler, [notification, cancellationToken]) is Task task)
                {
                    tasks.Add(task);
                }
                else
                {
                    (failures ??= new List<Exception>()).Add(new InvalidOperationException(
                        $"Handler {handler.GetType().Name} returned null instead of a Task from HandleAsync."));
                }
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                (failures ??= new List<Exception>()).Add(ex.InnerException);
            }
        }

        Exception? whenAllException = null;
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Task.WhenAll rethrows only the first exception; faulted tasks are drained below so every
            // failure is observed. The captured exception is preserved to cover the cancellation-only
            // case, where a canceled task is neither faulted nor represented in the drain loop.
            whenAllException = ex;
        }

        foreach (var task in tasks)
        {
            if (task.IsFaulted && task.Exception is not null)
            {
                (failures ??= new List<Exception>()).AddRange(task.Exception.InnerExceptions);
            }
        }

        if (failures is not null)
        {
            if (failures.Count == 1)
            {
                ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }

            throw new AggregateException(failures);
        }

        // No handler faulted, but a task may have been canceled - Task.WhenAll surfaced it; preserve that.
        if (whenAllException is not null)
        {
            ExceptionDispatchInfo.Capture(whenAllException).Throw();
        }
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