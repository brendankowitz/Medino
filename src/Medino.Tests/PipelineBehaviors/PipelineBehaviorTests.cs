using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;
using Medino.Tests.Requests;

namespace Medino.Tests.PipelineBehaviors;

public class PipelineBehaviorTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public PipelineBehaviorTests()
    {
        var services = new ServiceCollection();

        // Register behaviors as singleton so we can inspect state
        services.AddSingleton<IPipelineBehavior<object, TestResponse>, TestResponseLoggingBehavior>();
        services.AddSingleton<IPipelineBehavior<object, ValidatableResponse>, ValidatableObjectValidationBehavior>();

        services.AddMedino(typeof(PipelineBehaviorTests).Assembly);
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
    public async Task PipelineBehavior_ShouldExecuteBeforeAndAfterHandler()
    {
        // Arrange
        var behavior = _serviceProvider.GetServices<IPipelineBehavior<object, TestResponse>>()
            .OfType<TestResponseLoggingBehavior>()
            .FirstOrDefault();

        // Act
        var response = await _mediator.SendAsync(new TestRequest());

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(behavior);
        Assert.Equal(2, behavior!.Logs.Count);
        Assert.Contains("Before", behavior.Logs[0]);
        Assert.Contains("After", behavior.Logs[1]);
    }

    [Fact]
    public async Task ValidationBehavior_ShouldThrowValidationException_WhenInvalid()
    {
        // Arrange
        var request = new ValidatableRequest { IsValid = false };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(async () => await _mediator.SendAsync(request));
    }

    [Fact]
    public async Task ValidationBehavior_ShouldPassThrough_WhenValid()
    {
        // Arrange
        var request = new ValidatableRequest { IsValid = true };

        // Act
        var response = await _mediator.SendAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task PipelineBehavior_WithConcreteRequestType_ShouldExecuteMultipleBehaviors()
    {
        // Arrange - Create a new service provider with concrete request type behaviors
        var services = new ServiceCollection();

        // Register multiple behaviors for the specific TestRequest type (not object)
        services.AddSingleton<TestRequestLoggingBehavior>();
        services.AddSingleton<TestRequestTimingBehavior>();
        services.AddSingleton<IPipelineBehavior<TestRequest, TestResponse>>(sp => sp.GetRequiredService<TestRequestLoggingBehavior>());
        services.AddSingleton<IPipelineBehavior<TestRequest, TestResponse>>(sp => sp.GetRequiredService<TestRequestTimingBehavior>());

        services.AddMedino(typeof(PipelineBehaviorTests).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        var loggingBehavior = serviceProvider.GetRequiredService<TestRequestLoggingBehavior>();
        var timingBehavior = serviceProvider.GetRequiredService<TestRequestTimingBehavior>();

        // Act
        var response = await mediator.SendAsync(new TestRequest());

        // Assert
        Assert.NotNull(response);

        // Both behaviors should have executed
        Assert.Equal(2, loggingBehavior.Logs.Count);
        Assert.Contains("Before", loggingBehavior.Logs[0]);
        Assert.Contains("After", loggingBehavior.Logs[1]);

        Assert.True(timingBehavior.StartTime > DateTime.MinValue);
        Assert.True(timingBehavior.EndTime > timingBehavior.StartTime);

        // Cleanup
        (serviceProvider as IDisposable)?.Dispose();
    }
}