namespace Medino.Tests.Events.HandlerExceptions;

/// <summary>
/// Throws synchronously (non-async method) so the exception is raised while the handler
/// is being invoked, reproducing the lazy-enumeration orphaning bug.
/// </summary>
public class ThrowingSyncHandler : INotificationHandler<MultiHandlerEvent>
{
    public const string Message = "sync boom";

    public Task HandleAsync(MultiHandlerEvent notification, CancellationToken cancellationToken)
        => throw new InvalidOperationException(Message);
}
