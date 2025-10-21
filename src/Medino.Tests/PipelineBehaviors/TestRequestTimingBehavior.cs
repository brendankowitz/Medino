using Medino.Tests.Requests;

namespace Medino.Tests.PipelineBehaviors;

/// <summary>
/// Pipeline behavior that tracks timing for the concrete TestRequest type
/// </summary>
public class TestRequestTimingBehavior : IPipelineBehavior<TestRequest, TestResponse>
{
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }

    public async Task<TestResponse> HandleAsync(TestRequest request, RequestHandlerDelegate<TestResponse> next, CancellationToken cancellationToken)
    {
        StartTime = DateTime.UtcNow;
        var response = await next();
        EndTime = DateTime.UtcNow;
        return response;
    }
}
