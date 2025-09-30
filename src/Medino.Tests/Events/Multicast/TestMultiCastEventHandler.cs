namespace Medino.Tests.Events.Multicast;

public class TestMultiCastEventHandler : INotificationHandler<TestMultiCastEvent>
{
    public Task HandleAsync(TestMultiCastEvent notification, CancellationToken cancellationToken)
    {
        notification.WasHandled = true;
        return Task.CompletedTask;
    }
}