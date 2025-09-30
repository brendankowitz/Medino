using Medino.Tests.Requests;

namespace Medino.Tests.PipelineBehaviors;

public class TestResponseLoggingBehavior : IPipelineBehavior<object, TestResponse>
{
    public List<string> Logs { get; } = new();

    public async Task<TestResponse> HandleAsync(object request, RequestHandlerDelegate<TestResponse> next, CancellationToken cancellationToken)
    {
        Logs.Add($"Before: {request.GetType().Name}");
        var response = await next();
        Logs.Add($"After: {request.GetType().Name}");
        return response;
    }
}