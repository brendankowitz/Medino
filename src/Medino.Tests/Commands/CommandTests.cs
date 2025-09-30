namespace Medino.Tests.Commands;

[Collection("Mediator Tests")]
public class CommandTests
{
    private readonly IMediator _mediator;

    public CommandTests(GlobalSetup setup)
    {
        _mediator = setup.Mediator;
    }

    [Fact]
    public async Task GivenACommandIsCreated_WhenItIsSent_ThenTheHandlerIsCalled()
    {
        var command = new TestCommand();

        await _mediator.SendAsync(command);

        Assert.True(command.WasHandled);
    }
}