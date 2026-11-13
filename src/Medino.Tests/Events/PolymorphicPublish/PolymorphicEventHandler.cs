namespace Medino.Tests.Events.PolymorphicPublish;

public class PolymorphicEventHandler : INotificationHandler<PolymorphicEvent>
{
    public Task HandleAsync(PolymorphicEvent notification, CancellationToken cancellationToken)
    {
        notification.WasHandled = true;
        return Task.CompletedTask;
    }
}
