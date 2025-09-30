using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Medino;

/// <summary>
/// Default mediator implementation that encapsulates request/response and publishing interaction patterns
/// </summary>
public class Mediator : IMediator
{
    private readonly IMediatorServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of Mediator
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving services</param>
    public Mediator(IMediatorServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc />
    public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        if (command == null) throw new ArgumentNullException(nameof(command));

        var handler = _serviceProvider.GetService<ICommandHandler<TCommand>>();
        if (handler == null)
        {
            throw new InvalidOperationException($"No handler registered for command type {typeof(TCommand).Name}");
        }

        await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
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

        // Resolve handler using reflection (cannot use generic method since types are runtime)
        var getServiceMethod = typeof(IMediatorServiceProvider).GetMethod(nameof(IMediatorServiceProvider.GetService))!
            .MakeGenericMethod(handlerType);
        var handler = getServiceMethod.Invoke(_serviceProvider, null);

        if (handler == null)
        {
            throw new InvalidOperationException($"No handler registered for request type {requestType.Name}");
        }

        var handleMethod = handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync));
        if (handleMethod == null)
        {
            throw new InvalidOperationException($"HandleAsync method not found on handler {handlerType.Name}");
        }

        // Get context behaviors (for request transformation)
        var contextBehaviorType = typeof(IContextPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var getContextBehaviorsMethod = typeof(IMediatorServiceProvider).GetMethod(nameof(IMediatorServiceProvider.GetServices))!
            .MakeGenericMethod(contextBehaviorType);
        var contextBehaviors = ((IEnumerable<object>)getContextBehaviorsMethod.Invoke(_serviceProvider, null)!).ToList();

        // Get regular pipeline behaviors
        var behaviors = _serviceProvider.GetServices<IPipelineBehavior<object, TResponse>>().ToList();

        if (contextBehaviors.Any() || behaviors.Any())
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
                    var result = handleMethod.Invoke(handler, new object[] { currentRequest!, cancellationToken });
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
                var behavior = behaviors[i];
                var next = pipeline;
                pipeline = () =>
                {
                    var currentRequest = contextRequestProperty.GetValue(context);
                    return behavior.HandleAsync(currentRequest!, next, cancellationToken);
                };
            }

            // Add context behaviors (execute first, can transform request)
            for (var i = contextBehaviors.Count - 1; i >= 0; i--)
            {
                var contextBehavior = contextBehaviors[i];
                var next = pipeline;

                // Use reflection to call HandleAsync on the context behavior
                var behaviorType = contextBehavior.GetType();
                var handleAsyncMethod = behaviorType.GetMethod(nameof(IContextPipelineBehavior<object, TResponse>.HandleAsync));

                if (handleAsyncMethod != null)
                {
                    pipeline = () =>
                    {
                        try
                        {
                            var result = handleAsyncMethod.Invoke(contextBehavior, new object[] { context, next, cancellationToken });
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
                var result = handleMethod.Invoke(handler, new object[] { request, cancellationToken });
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

        var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>().ToList();

        if (!handlers.Any())
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
        var getServicesMethod = typeof(IMediatorServiceProvider).GetMethod(nameof(IMediatorServiceProvider.GetServices))!
            .MakeGenericMethod(handlerInterfaceType);
        var handlers = ((IEnumerable<object>)getServicesMethod.Invoke(_serviceProvider, null)!).ToList();

        if (handlers.Any())
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
        var getServicesMethod = typeof(IMediatorServiceProvider).GetMethod(nameof(IMediatorServiceProvider.GetServices))!
            .MakeGenericMethod(actionInterfaceType);
        var actions = ((IEnumerable<object>)getServicesMethod.Invoke(_serviceProvider, null)!).ToList();

        if (actions.Any())
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
}