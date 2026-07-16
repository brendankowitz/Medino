namespace Medino.Tests.Events.HandlerExceptions;

public class CancellationHonoringHandler : INotificationHandler<MultiHandlerEvent>
{
    public async Task HandleAsync(MultiHandlerEvent notification, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
    }
}
