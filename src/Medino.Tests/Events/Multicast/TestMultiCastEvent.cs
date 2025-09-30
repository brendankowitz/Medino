namespace Medino.Tests.Events.Multicast;

public class TestMultiCastEvent : INotification
{
    public bool WasHandled { get; set; }
    public bool SecondHandlerWasCalled { get; set; }
}