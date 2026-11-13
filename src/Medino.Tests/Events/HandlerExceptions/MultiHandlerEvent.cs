namespace Medino.Tests.Events.HandlerExceptions;

public class MultiHandlerEvent : INotification
{
    public bool RecordingHandlerRan { get; set; }
}
