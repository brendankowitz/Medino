using Microsoft.Extensions.DependencyInjection;

namespace Medino.Tests.Events.HandlerExceptions;

public class HandlerExceptionTests
{
    [Fact]
    public async Task GivenAHandlerThrowsSynchronously_ThenLaterHandlersStillRun()
    {
        var services = new ServiceCollection();
        // Throwing handler registered first so it is enumerated before the recording handler.
        services.AddTransient<INotificationHandler<MultiHandlerEvent>, ThrowingSyncHandler>();
        services.AddTransient<INotificationHandler<MultiHandlerEvent>, RecordingHandler>();
        using var provider = services.BuildServiceProvider();
        var mediator = new Medino.Mediator(provider);

        var notification = new MultiHandlerEvent();

        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.PublishAsync(notification));

        Assert.True(notification.RecordingHandlerRan);
    }

    [Fact]
    public async Task GivenMultipleHandlersThrow_ThenAllExceptionsAreObserved()
    {
        var services = new ServiceCollection();
        services.AddTransient<INotificationHandler<MultiHandlerEvent>, ThrowingSyncHandler>();
        services.AddTransient<INotificationHandler<MultiHandlerEvent>, SecondThrowingSyncHandler>();
        using var provider = services.BuildServiceProvider();
        var mediator = new Medino.Mediator(provider);

        var aggregate = await Assert.ThrowsAsync<AggregateException>(
            () => mediator.PublishAsync(new MultiHandlerEvent()));

        Assert.Contains(aggregate.InnerExceptions, e => e is InvalidOperationException);
        Assert.Contains(aggregate.InnerExceptions, e => e is NotSupportedException);
    }

    [Fact]
    public async Task GivenAnAsyncHandlerFaultsAfterAwaiting_ThenLaterHandlersStillRunAndTheExceptionSurfaces()
    {
        var services = new ServiceCollection();
        services.AddTransient<INotificationHandler<MultiHandlerEvent>, AsyncThrowingHandler>();
        services.AddTransient<INotificationHandler<MultiHandlerEvent>, RecordingHandler>();
        using var provider = services.BuildServiceProvider();
        var mediator = new Medino.Mediator(provider);

        var notification = new MultiHandlerEvent();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.PublishAsync(notification));

        Assert.Equal(AsyncThrowingHandler.Message, ex.Message);
        Assert.True(notification.RecordingHandlerRan);
    }

    [Fact]
    public async Task GivenAHandlerObservesCancellation_ThenTheCancellationPropagates()
    {
        var services = new ServiceCollection();
        services.AddTransient<INotificationHandler<MultiHandlerEvent>, CancellationHonoringHandler>();
        using var provider = services.BuildServiceProvider();
        var mediator = new Medino.Mediator(provider);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => mediator.PublishAsync(new MultiHandlerEvent(), cts.Token));
    }

    [Fact]
    public async Task GivenAHandlerReturnsNull_ThenAnActionableExceptionIsThrown()
    {
        var services = new ServiceCollection();
        services.AddTransient<INotificationHandler<MultiHandlerEvent>, NullReturningHandler>();
        using var provider = services.BuildServiceProvider();
        var mediator = new Medino.Mediator(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.PublishAsync(new MultiHandlerEvent()));
    }
}
