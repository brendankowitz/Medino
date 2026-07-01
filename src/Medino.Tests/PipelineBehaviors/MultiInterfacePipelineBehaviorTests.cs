using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;
using Medino.Tests.Requests;

namespace Medino.Tests.PipelineBehaviors;

/// <summary>
/// Regression tests for a single pipeline behavior class implementing IPipelineBehavior&lt;,&gt;
/// for more than one closed-generic request type. Resolving HandleAsync via the behavior's
/// concrete Type (rather than the specific interface it was resolved for) previously threw
/// AmbiguousMatchException, since the concrete type declares more than one public HandleAsync
/// overload in that scenario.
/// </summary>
public class MultiInterfacePipelineBehaviorTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly MultiRequestTypeBehavior _behavior;

    public MultiInterfacePipelineBehaviorTests()
    {
        var services = new ServiceCollection();

        _behavior = new MultiRequestTypeBehavior();
        services.AddSingleton(_behavior);
        services.AddSingleton<IPipelineBehavior<TestRequest, TestResponse>>(sp => sp.GetRequiredService<MultiRequestTypeBehavior>());
        services.AddSingleton<IPipelineBehavior<AnotherTestRequest, TestResponse>>(sp => sp.GetRequiredService<MultiRequestTypeBehavior>());

        services.AddMedino(typeof(MultiInterfacePipelineBehaviorTests).Assembly);
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }

    [Fact]
    public async Task GivenBehaviorImplementingMultipleClosedGenericInterfaces_WhenSendingFirstRequestType_ThenHandleAsyncIsInvokedWithoutAmbiguity()
    {
        var response = await _mediator.SendAsync(new TestRequest());

        Assert.NotNull(response);
        Assert.Equal(1, _behavior.TestRequestHandleCount);
        Assert.Equal(0, _behavior.AnotherTestRequestHandleCount);
    }

    [Fact]
    public async Task GivenBehaviorImplementingMultipleClosedGenericInterfaces_WhenSendingSecondRequestType_ThenHandleAsyncIsInvokedWithoutAmbiguity()
    {
        var response = await _mediator.SendAsync(new AnotherTestRequest());

        Assert.NotNull(response);
        Assert.Equal(0, _behavior.TestRequestHandleCount);
        Assert.Equal(1, _behavior.AnotherTestRequestHandleCount);
    }
}

public record AnotherTestRequest : IRequest<TestResponse>;

public class AnotherTestRequestHandler : IRequestHandler<AnotherTestRequest, TestResponse>
{
    public Task<TestResponse> HandleAsync(AnotherTestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new TestResponse());
    }
}

/// <summary>
/// A single behavior implementing IPipelineBehavior&lt;,&gt; for two different request types
/// sharing the same response type - mirroring real-world usages where one behavior class
/// applies shared logic (e.g. search parameter bookkeeping) across a Create and an Upsert request.
/// </summary>
public class MultiRequestTypeBehavior : IPipelineBehavior<TestRequest, TestResponse>, IPipelineBehavior<AnotherTestRequest, TestResponse>
{
    public int TestRequestHandleCount { get; private set; }

    public int AnotherTestRequestHandleCount { get; private set; }

    public Task<TestResponse> HandleAsync(TestRequest request, RequestHandlerDelegate<TestResponse> next, CancellationToken cancellationToken)
    {
        TestRequestHandleCount++;
        return next();
    }

    public Task<TestResponse> HandleAsync(AnotherTestRequest request, RequestHandlerDelegate<TestResponse> next, CancellationToken cancellationToken)
    {
        AnotherTestRequestHandleCount++;
        return next();
    }
}
