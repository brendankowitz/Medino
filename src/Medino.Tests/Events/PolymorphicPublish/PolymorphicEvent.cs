namespace Medino.Tests.Events.PolymorphicPublish;

public class PolymorphicEvent : INotification
{
    public bool WasHandled { get; set; }
}
