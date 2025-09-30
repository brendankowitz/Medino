namespace Medino.Tests.Events.Multicast;

public class AnotherTestMultiCastEventHandler : INotificationHandler<TestMultiCastEvent>
{
    public Task HandleAsync(TestMultiCastEvent notification, CancellationToken cancellationToken)
    {
        notification.SecondHandlerWasCalled = true;
        return Task.CompletedTask;
    }
}