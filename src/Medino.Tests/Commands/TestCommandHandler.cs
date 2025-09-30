namespace Medino.Tests.Commands;

public class TestCommandHandler : ICommandHandler<TestCommand>
{
    public Task HandleAsync(TestCommand command, CancellationToken cancellationToken)
    {
        command.WasHandled = true;
        return Task.CompletedTask;
    }
}