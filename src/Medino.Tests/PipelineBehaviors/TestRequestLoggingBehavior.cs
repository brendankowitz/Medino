using Medino.Tests.Requests;

namespace Medino.Tests.PipelineBehaviors;

/// <summary>
/// Pipeline behavior for the concrete TestRequest type (not object)
/// </summary>
public class TestRequestLoggingBehavior : IPipelineBehavior<TestRequest, TestResponse>
{
    public List<string> Logs { get; } = new();

    public async Task<TestResponse> HandleAsync(TestRequest request, RequestHandlerDelegate<TestResponse> next, CancellationToken cancellationToken)
    {
        Logs.Add($"Before: {request.GetType().Name}");
        var response = await next();
        Logs.Add($"After: {request.GetType().Name}");
        return response;
    }
}
