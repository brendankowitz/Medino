namespace Medino.Tests.Events.Multicast;

[Collection("Mediator Tests")]
public class EventTests
{
    private readonly IMediator _mediator;

    public EventTests(GlobalSetup setup)
    {
        _mediator = setup.Mediator;
    }

    [Fact]
    public async Task GivenANotificationIsCreated_WhenItIsPublished_ThenAllHandlersAreCalled()
    {
        var notification = new TestMultiCastEvent();

        await _mediator.PublishAsync(notification);

        Assert.True(notification.WasHandled);
        Assert.True(notification.SecondHandlerWasCalled);
    }
}