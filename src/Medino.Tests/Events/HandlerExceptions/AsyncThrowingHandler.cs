namespace Medino.Tests.Events.HandlerExceptions;

/// <summary>
/// Faults after its first await, so the failure is observed via a faulted Task rather than a
/// synchronous throw - exercising the drain-and-aggregate path.
/// </summary>
public class AsyncThrowingHandler : INotificationHandler<MultiHandlerEvent>
{
    public const string Message = "async boom";

    public async Task HandleAsync(MultiHandlerEvent notification, CancellationToken cancellationToken)
    {
        await Task.Yield();
        throw new InvalidOperationException(Message);
    }
}
