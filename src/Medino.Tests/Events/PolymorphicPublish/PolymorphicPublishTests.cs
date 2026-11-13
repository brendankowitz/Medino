using Microsoft.Extensions.DependencyInjection;

namespace Medino.Tests.Events.PolymorphicPublish;

public class PolymorphicPublishTests
{
    [Fact]
    public async Task GivenANotificationPublishedThroughItsInterface_ThenTheConcreteHandlerIsInvoked()
    {
        var services = new ServiceCollection();
        services.AddTransient<INotificationHandler<PolymorphicEvent>, PolymorphicEventHandler>();
        using var provider = services.BuildServiceProvider();
        var mediator = new Medino.Mediator(provider);

        var concrete = new PolymorphicEvent();
        INotification notification = concrete; // published via the base interface, not the concrete type

        await mediator.PublishAsync(notification);

        Assert.True(concrete.WasHandled);
    }
}
