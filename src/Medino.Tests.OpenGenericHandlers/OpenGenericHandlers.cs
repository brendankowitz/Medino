namespace Medino.Tests.OpenGenericHandlers;

// Open generic handlers must be skipped by assembly scanning. They live in their own
// assembly so other tests that scan the main test assembly are unaffected.
public record ClosedCommand : ICommand;

public record ClosedNotification : INotification;

public class ClosedCommandHandler : ICommandHandler<ClosedCommand>
{
    public Task HandleAsync(ClosedCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class GenericCommandHandler<T> : ICommandHandler<T> where T : ICommand
{
    public Task HandleAsync(T command, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class GenericNotificationHandler<T> : INotificationHandler<T> where T : INotification
{
    public Task HandleAsync(T notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
