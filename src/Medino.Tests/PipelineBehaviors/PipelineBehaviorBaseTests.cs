using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;

namespace Medino.Tests.PipelineBehaviors;

public class PipelineBehaviorBaseTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public PipelineBehaviorBaseTests()
    {
        var services = new ServiceCollection();

        // Register behaviors as singleton so we can inspect state
        services.AddSingleton<IPipelineBehavior<object, string>, TestLoggingBehavior>();
        services.AddSingleton<IPipelineBehavior<object, string>, TestValidationBehavior>();
        services.AddSingleton<IPipelineBehavior<object, string>, TestCachingBehavior>();

        services.AddMedino(typeof(PipelineBehaviorBaseTests).Assembly);
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task PipelineBehaviorBase_ShouldExecuteBeforeAndAfter()
    {
        // Arrange
        var behavior = _serviceProvider.GetServices<IPipelineBehavior<object, string>>()
            .OfType<TestLoggingBehavior>()
            .First();
        var request = new BaseTestRequest { Value = "test" };

        // Act
        var response = await _mediator.SendAsync(request);

        // Assert
        Assert.Equal("Handled: test", response);
        Assert.True(behavior.BeforeCalled);
        Assert.True(behavior.AfterCalled);
        Assert.Equal("Before,After", behavior.ExecutionOrder);
    }

    [Fact]
    public async Task BeforePipelineBehavior_ShouldOnlyExecuteBefore()
    {
        // Arrange
        var behavior = _serviceProvider.GetServices<IPipelineBehavior<object, string>>()
            .OfType<TestValidationBehavior>()
            .First();
        var request = new BaseTestRequest { Value = "valid" };

        // Act
        var response = await _mediator.SendAsync(request);

        // Assert
        Assert.Equal("Handled: valid", response);
        Assert.True(behavior.BeforeCalled);
    }

    [Fact]
    public async Task AfterPipelineBehavior_ShouldOnlyExecuteAfter()
    {
        // Arrange
        var behavior = _serviceProvider.GetServices<IPipelineBehavior<object, string>>()
            .OfType<TestCachingBehavior>()
            .First();
        var request = new BaseTestRequest { Value = "cacheable" };

        // Act
        var response = await _mediator.SendAsync(request);

        // Assert
        Assert.Equal("Handled: cacheable", response);
        Assert.True(behavior.AfterCalled);
        Assert.Equal("Handled: cacheable", behavior.CachedResponse);
    }

    [Fact]
    public async Task BeforePipelineBehavior_CanThrowValidationException()
    {
        // Arrange
        var request = new BaseTestRequest { Value = "invalid" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await _mediator.SendAsync(request));
        Assert.Contains("Invalid value", ex.Message);
    }
}

// Test request and handler
public record BaseTestRequest : IRequest<string>
{
    public string Value { get; init; } = string.Empty;
}

public class BaseTestRequestHandler : IRequestHandler<BaseTestRequest, string>
{
    public Task<string> HandleAsync(BaseTestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Handled: {request.Value}");
    }
}

// Example: Full before/after behavior
public class TestLoggingBehavior : PipelineBehaviorBase<object, string>
{
    public bool BeforeCalled { get; private set; }
    public bool AfterCalled { get; private set; }
    public string ExecutionOrder { get; private set; } = string.Empty;

    protected override Task BeforeAsync(object request, CancellationToken cancellationToken)
    {
        BeforeCalled = true;
        ExecutionOrder = "Before";
        return Task.CompletedTask;
    }

    protected override Task AfterAsync(object request, string response, CancellationToken cancellationToken)
    {
        AfterCalled = true;
        ExecutionOrder += ",After";
        return Task.CompletedTask;
    }
}

// Example: Before-only behavior (validation)
public class TestValidationBehavior : BeforePipelineBehavior<object, string>
{
    public bool BeforeCalled { get; private set; }

    protected override Task BeforeAsync(object request, CancellationToken cancellationToken)
    {
        BeforeCalled = true;

        if (request is BaseTestRequest { Value: "invalid" })
        {
            throw new ArgumentException("Invalid value");
        }

        return Task.CompletedTask;
    }
}

// Example: After-only behavior (caching)
public class TestCachingBehavior : AfterPipelineBehavior<object, string>
{
    public bool AfterCalled { get; private set; }
    public string? CachedResponse { get; private set; }

    protected override Task AfterAsync(object request, string response, CancellationToken cancellationToken)
    {
        AfterCalled = true;
        CachedResponse = response;
        return Task.CompletedTask;
    }
}