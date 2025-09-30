namespace Medino.Tests.PipelineBehaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public List<string> Logs { get; } = new();

    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Logs.Add($"Before: {typeof(TRequest).Name}");
        var response = await next();
        Logs.Add($"After: {typeof(TRequest).Name}");
        return response;
    }
}