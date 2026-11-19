namespace Medino.Tests.DuplicateHandlers;

// Deliberately invalid: two handlers for the same command and the same request.
// Lives in its own assembly so scanning the main test assembly stays valid.
public record DuplicatedCommand : ICommand;

public record DuplicatedRequest : IRequest<string>;

public class FirstDuplicatedCommandHandler : ICommandHandler<DuplicatedCommand>
{
    public Task HandleAsync(DuplicatedCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class SecondDuplicatedCommandHandler : ICommandHandler<DuplicatedCommand>
{
    public Task HandleAsync(DuplicatedCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FirstDuplicatedRequestHandler : IRequestHandler<DuplicatedRequest, string>
{
    public Task<string> HandleAsync(DuplicatedRequest request, CancellationToken cancellationToken = default) => Task.FromResult("first");
}

public class SecondDuplicatedRequestHandler : IRequestHandler<DuplicatedRequest, string>
{
    public Task<string> HandleAsync(DuplicatedRequest request, CancellationToken cancellationToken = default) => Task.FromResult("second");
}
