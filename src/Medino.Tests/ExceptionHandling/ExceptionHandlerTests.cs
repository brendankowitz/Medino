using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;

namespace Medino.Tests.ExceptionHandling;

public class ExceptionHandlerTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public ExceptionHandlerTests()
    {
        var services = new ServiceCollection();

        // Register exception action as singleton so we can inspect state
        services.AddSingleton<IRequestExceptionAction<UnhandledExceptionRequest, InvalidOperationException>, LogExceptionAction>();

        services.AddMedino(typeof(ExceptionHandlerTests).Assembly);
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
    public async Task ExceptionHandler_ShouldHandleException_AndReturnFallbackResponse()
    {
        // Arrange
        var request = new ThrowingRequest { ShouldThrow = true };

        // Act
        var response = await _mediator.SendAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.IsError);
        Assert.Equal("Handled by exception handler", response.ErrorMessage);
    }

    [Fact]
    public async Task ExceptionHandler_ShouldNotHandle_WhenNoException()
    {
        // Arrange
        var request = new ThrowingRequest { ShouldThrow = false };

        // Act
        var response = await _mediator.SendAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.IsError);
        Assert.Null(response.ErrorMessage);
    }

    [Fact]
    public async Task ExceptionAction_ShouldBeExecuted_WhenExceptionOccurs()
    {
        // Arrange
        var request = new UnhandledExceptionRequest();
        var action = _serviceProvider.GetServices<IRequestExceptionAction<UnhandledExceptionRequest, InvalidOperationException>>()
            .OfType<LogExceptionAction>()
            .FirstOrDefault();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _mediator.SendAsync(request));

        // Verify action was called
        Assert.NotNull(action);
        Assert.Equal(1, action!.ExecutionCount);
    }

    [Fact]
    public async Task ExceptionAction_ShouldTranslateException_WhenActionThrows()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IRequestExceptionAction<TranslateExceptionRequest, InvalidOperationException>, ExceptionTranslationAction>();
        services.AddMedino(typeof(ExceptionHandlerTests).Assembly);
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var request = new TranslateExceptionRequest();

        // Act & Assert - should throw ArgumentException (translated) not InvalidOperationException (original)
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () => await mediator.SendAsync(request));
        Assert.Equal("Translated exception", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal("Original exception", ex.InnerException!.Message);
    }
}

// Test requests and handlers
public record ThrowingRequest : IRequest<ThrowingResponse>
{
    public bool ShouldThrow { get; set; }
}

public class ThrowingResponse
{
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ThrowingRequestHandler : IRequestHandler<ThrowingRequest, ThrowingResponse>
{
    public Task<ThrowingResponse> HandleAsync(ThrowingRequest request, CancellationToken cancellationToken)
    {
        if (request.ShouldThrow)
        {
            throw new InvalidOperationException("Something went wrong");
        }

        return Task.FromResult(new ThrowingResponse { IsError = false });
    }
}

public class ThrowingExceptionHandler : IRequestExceptionHandler<ThrowingRequest, ThrowingResponse, InvalidOperationException>
{
    public Task HandleAsync(ThrowingRequest request, InvalidOperationException exception, RequestExceptionHandlerState<ThrowingResponse> state, CancellationToken cancellationToken)
    {
        state.SetHandled(new ThrowingResponse
        {
            IsError = true,
            ErrorMessage = "Handled by exception handler"
        });
        return Task.CompletedTask;
    }
}

// Unhandled exception test
public record UnhandledExceptionRequest : IRequest<string>;

public class UnhandledExceptionRequestHandler : IRequestHandler<UnhandledExceptionRequest, string>
{
    public Task<string> HandleAsync(UnhandledExceptionRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Unhandled exception");
    }
}

public class LogExceptionAction : IRequestExceptionAction<UnhandledExceptionRequest, InvalidOperationException>
{
    public int ExecutionCount { get; private set; }

    public Task ExecuteAsync(UnhandledExceptionRequest request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        ExecutionCount++;
        return Task.CompletedTask;
    }
}

// Exception translation test
public record TranslateExceptionRequest : IRequest<string>;

public class TranslateExceptionRequestHandler : IRequestHandler<TranslateExceptionRequest, string>
{
    public Task<string> HandleAsync(TranslateExceptionRequest request, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Original exception");
    }
}

public class ExceptionTranslationAction : IRequestExceptionAction<TranslateExceptionRequest, InvalidOperationException>
{
    public Task ExecuteAsync(TranslateExceptionRequest request, InvalidOperationException exception, CancellationToken cancellationToken)
    {
        // Translate to a different exception type
        throw new ArgumentException("Translated exception", exception);
    }
}