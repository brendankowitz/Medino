namespace Medino.Tests.Events.HandlerExceptions;

public class NullReturningHandler : INotificationHandler<MultiHandlerEvent>
{
    public Task HandleAsync(MultiHandlerEvent notification, CancellationToken cancellationToken) => null!;
}
