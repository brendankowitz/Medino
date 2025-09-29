namespace Medino.Tests.Requests;

public class TestRequestHandler : IRequestHandler<TestRequest, TestResponse>
{
    public Task<TestResponse> HandleAsync(TestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new TestResponse());
    }
}