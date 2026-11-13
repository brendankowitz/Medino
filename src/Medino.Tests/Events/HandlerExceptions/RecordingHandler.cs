namespace Medino.Tests.Events.HandlerExceptions;

public class RecordingHandler : INotificationHandler<MultiHandlerEvent>
{
    public Task HandleAsync(MultiHandlerEvent notification, CancellationToken cancellationToken)
    {
        notification.RecordingHandlerRan = true;
        return Task.CompletedTask;
    }
}
