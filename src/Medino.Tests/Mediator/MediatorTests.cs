using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;

namespace Medino.Tests.Mediator;

public class MediatorTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public MediatorTests()
    {
        var services = new ServiceCollection();
        services.AddMedino(typeof(MediatorTests).Assembly);
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
    public async Task SendAsync_Command_ShouldInvokeHandler()
    {
        // Arrange
        var command = new CounterCommand();

        // Act
        await _mediator.SendAsync(command);

        // Assert
        Assert.True(command.WasHandled);
    }

    [Fact]
    public async Task SendAsync_Query_ShouldReturnResponse()
    {
        // Arrange
        var query = new GetNumberQuery { Number = 42 };

        // Act
        var result = await _mediator.SendAsync(query);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task PublishAsync_Notification_ShouldInvokeAllHandlers()
    {
        // Arrange
        var notification = new CounterNotification();

        // Act
        await _mediator.PublishAsync(notification);

        // Assert
        Assert.Equal(2, notification.HandlerCount);
    }

    [Fact]
    public async Task PublishAsync_NotificationWithNoHandlers_ShouldNotThrow()
    {
        // Arrange
        var notification = new NoHandlerNotification();

        // Act (should not throw)
        await _mediator.PublishAsync(notification);
    }

    [Fact]
    public async Task SendAsync_Command_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        CounterCommand? nullCommand = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _mediator.SendAsync(nullCommand!));
    }

    [Fact]
    public async Task SendAsync_Query_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _mediator.SendAsync<int>(null!));
    }

    [Fact]
    public async Task PublishAsync_WithNullNotification_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _mediator.PublishAsync<CounterNotification>(null!));
    }

    [Fact]
    public async Task SendAsync_WithNoHandler_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var command = new NoHandlerCommand();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await _mediator.SendAsync(command));
        Assert.StartsWith($"No service of type {typeof(ICommandHandler<NoHandlerCommand>).FullName}", ex.Message);
    }

    [Fact]
    public async Task SendAsync_WithCancellationToken_ShouldPassToken()
    {
        // Arrange
        var query = new CancellableQuery();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await _mediator.SendAsync(query, cts.Token));
    }
}

// Test types
public class CounterCommand : ICommand
{
    public bool WasHandled { get; set; }
}

public class CounterCommandHandler : ICommandHandler<CounterCommand>
{
    public Task HandleAsync(CounterCommand command, CancellationToken cancellationToken)
    {
        command.WasHandled = true;
        return Task.CompletedTask;
    }
}

public record GetNumberQuery : IRequest<int>
{
    public int Number { get; init; }
}

public class GetNumberQueryHandler : IRequestHandler<GetNumberQuery, int>
{
    public Task<int> HandleAsync(GetNumberQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.Number);
    }
}

public class CounterNotification : INotification
{
    public int HandlerCount { get; set; }
}

public class FirstCounterNotificationHandler : INotificationHandler<CounterNotification>
{
    public Task HandleAsync(CounterNotification notification, CancellationToken cancellationToken)
    {
        notification.HandlerCount++;
        return Task.CompletedTask;
    }
}

public class SecondCounterNotificationHandler : INotificationHandler<CounterNotification>
{
    public Task HandleAsync(CounterNotification notification, CancellationToken cancellationToken)
    {
        notification.HandlerCount++;
        return Task.CompletedTask;
    }
}

public record NoHandlerNotification : INotification;

public record NoHandlerCommand : ICommand;

public record CancellableQuery : IRequest<string>;

public class CancellableQueryHandler : IRequestHandler<CancellableQuery, string>
{
    public Task<string> HandleAsync(CancellableQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("Success");
    }
}