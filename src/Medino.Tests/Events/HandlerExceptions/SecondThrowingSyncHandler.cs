namespace Medino.Tests.Events.HandlerExceptions;

public class SecondThrowingSyncHandler : INotificationHandler<MultiHandlerEvent>
{
    public const string Message = "second boom";

    public Task HandleAsync(MultiHandlerEvent notification, CancellationToken cancellationToken)
        => throw new NotSupportedException(Message);
}
