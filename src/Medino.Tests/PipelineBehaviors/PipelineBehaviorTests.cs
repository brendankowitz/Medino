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

    // Regression test for AmbiguousMatchException when a single class implements more than one
    // closed IPipelineBehavior<,> interface.
    //
    // The mediator invokes a behavior's HandleAsync via reflection. It previously resolved the method
    // with concreteType.GetMethod("HandleAsync"), which inspects the implementation type. When that
    // type declares two HandleAsync overloads (one per closed interface it implements), GetMethod finds
    // more than one match and throws AmbiguousMatchException before the handler ever runs.
    //
    // The fix resolves HandleAsync on the *closed interface* the behavior was registered as
    // (IPipelineBehavior<OverloadedSecondRequest, string>), which exposes exactly one HandleAsync,
    // so the correct overload is selected for each request type.
    //
    // The OverloadedPipelineBehavior below reproduces the failing shape (one class, two closed
    // interfaces, two overloads), registered as a shared singleton behind both interfaces.
    [Fact]
    public async Task PipelineBehavior_WithMultipleClosedHandleAsyncOverloads_ShouldInvokeMatchingOverload()
    {
        // Arrange - register one shared instance behind both closed IPipelineBehavior<,> interfaces.
        // The shared instance lets a single InvokedOverload field record which overload actually ran.
        var services = new ServiceCollection();
        services.AddSingleton<OverloadedPipelineBehavior>();
        services.AddSingleton<IPipelineBehavior<OverloadedFirstRequest, string>>(sp => sp.GetRequiredService<OverloadedPipelineBehavior>());
        services.AddSingleton<IPipelineBehavior<OverloadedSecondRequest, string>>(sp => sp.GetRequiredService<OverloadedPipelineBehavior>());
        services.AddMedino(typeof(PipelineBehaviorTests).Assembly);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var behavior = serviceProvider.GetRequiredService<OverloadedPipelineBehavior>();

        // Act & Assert - send each request type and confirm its matching HandleAsync overload ran.
        // Both directions are exercised so a fix that hard-coded a single overload would still fail.
        var secondResponse = await mediator.SendAsync(new OverloadedSecondRequest("second"));
        Assert.Equal("Handled second", secondResponse);
        Assert.Equal("second", behavior.InvokedOverload);

        var firstResponse = await mediator.SendAsync(new OverloadedFirstRequest("first"));
        Assert.Equal("Handled first", firstResponse);
        Assert.Equal("first", behavior.InvokedOverload);

        // Cleanup
        (serviceProvider as IDisposable)?.Dispose();
    }
}

public record OverloadedFirstRequest(string Value) : IRequest<string>;

public record OverloadedSecondRequest(string Value) : IRequest<string>;

public class OverloadedFirstRequestHandler : IRequestHandler<OverloadedFirstRequest, string>
{
    public Task<string> HandleAsync(OverloadedFirstRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Handled {request.Value}");
    }
}

public class OverloadedSecondRequestHandler : IRequestHandler<OverloadedSecondRequest, string>
{
    public Task<string> HandleAsync(OverloadedSecondRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Handled {request.Value}");
    }
}

// Implements two closed IPipelineBehavior<,> interfaces, so the concrete type declares two
// HandleAsync overloads. This is the exact shape that triggered AmbiguousMatchException when the
// mediator resolved HandleAsync by name on the concrete type.
public class OverloadedPipelineBehavior :
    IPipelineBehavior<OverloadedFirstRequest, string>,
    IPipelineBehavior<OverloadedSecondRequest, string>
{
    // Records which overload the mediator actually invoked, so the test can assert correct dispatch.
    public string? InvokedOverload { get; private set; }

    public async Task<string> HandleAsync(OverloadedFirstRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
    {
        InvokedOverload = "first";
        return await next();
    }

    public async Task<string> HandleAsync(OverloadedSecondRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
    {
        InvokedOverload = "second";
        return await next();
    }
}
