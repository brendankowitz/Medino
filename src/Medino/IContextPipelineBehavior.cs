namespace Medino;

/// <summary>
/// Context-aware pipeline behavior that can transform the request and enrich the pipeline context.
/// Context behaviors execute before regular pipeline behaviors, allowing request transformation.
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public interface IContextPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    /// <summary>
    /// Pipeline handler with access to mutable context.
    /// The context allows replacing the request and adding metadata that flows through the pipeline.
    /// </summary>
    /// <param name="context">The pipeline context containing the request and metadata</param>
    /// <param name="next">Awaitable delegate for the next action in the pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Awaitable task returning the <typeparamref name="TResponse"/></returns>
    Task<TResponse> HandleAsync(
        PipelineContext<TRequest> context,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}