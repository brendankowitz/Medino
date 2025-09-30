namespace Medino;

/// <summary>
/// Base class for pipeline behaviors that simplifies implementation by separating before and after logic
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public abstract class PipelineBehaviorBase<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Pipeline handler that calls Before, executes the handler, then calls After
    /// </summary>
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        await BeforeAsync(request, cancellationToken).ConfigureAwait(false);
        var response = await next().ConfigureAwait(false);
        await AfterAsync(request, response, cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// Called before the handler executes. Override to add pre-execution logic.
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected virtual Task BeforeAsync(TRequest request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called after the handler executes. Override to add post-execution logic.
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="response">The response from the handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected virtual Task AfterAsync(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Base class for pipeline behaviors that only need to execute logic before the handler
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public abstract class BeforePipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Pipeline handler that calls Before then executes the handler
    /// </summary>
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        await BeforeAsync(request, cancellationToken).ConfigureAwait(false);
        return await next().ConfigureAwait(false);
    }

    /// <summary>
    /// Called before the handler executes
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task BeforeAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for pipeline behaviors that only need to execute logic after the handler
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public abstract class AfterPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Pipeline handler that executes the handler then calls After
    /// </summary>
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next().ConfigureAwait(false);
        await AfterAsync(request, response, cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// Called after the handler executes
    /// </summary>
    /// <param name="request">The request</param>
    /// <param name="response">The response from the handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task AfterAsync(TRequest request, TResponse response, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for context-aware pipeline behaviors that transform the request
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public abstract class RequestTransformBehavior<TRequest, TResponse> : IContextPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Pipeline handler that transforms the request then continues
    /// </summary>
    public async Task<TResponse> HandleAsync(
        PipelineContext<TRequest> context,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        context.Request = await TransformAsync(context.Request, cancellationToken).ConfigureAwait(false);
        return await next().ConfigureAwait(false);
    }

    /// <summary>
    /// Transform the request and return the modified version.
    /// This is useful for normalizing data, applying defaults, or enriching with context.
    /// </summary>
    /// <param name="request">The original request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The transformed request</returns>
    protected abstract Task<TRequest> TransformAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for context-aware pipeline behaviors that enrich the context with metadata
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public abstract class RequestEnrichmentBehavior<TRequest, TResponse> : IContextPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Pipeline handler that enriches the context then continues
    /// </summary>
    public async Task<TResponse> HandleAsync(
        PipelineContext<TRequest> context,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        await EnrichAsync(context, cancellationToken).ConfigureAwait(false);
        return await next().ConfigureAwait(false);
    }

    /// <summary>
    /// Enrich the pipeline context with metadata.
    /// This can include correlation IDs, user context, tenant information, etc.
    /// The context can also be used to replace the request if needed.
    /// </summary>
    /// <param name="context">The pipeline context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task EnrichAsync(PipelineContext<TRequest> context, CancellationToken cancellationToken);
}