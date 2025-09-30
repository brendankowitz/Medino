# Medino

A lightweight, high-performance in-process mediator for .NET 8+ that implements the mediator pattern for CQRS, commands, queries, events, and pipeline behaviors.

## Features

- **Zero Dependencies**: Core library has no external dependencies
- **Commands & Queries**: Fire-and-forget commands and request/response queries
- **Events**: Pub/sub notifications with multiple handlers
- **Pipeline Behaviors**: Cross-cutting concerns like logging, validation, and caching
- **Context Behaviors**: Transform and enrich requests before processing
- **Exception Handling**: Graceful error handling with fallback responses
- **Explicit Async**: All methods clearly indicate async behavior with `*Async` suffix
- **MediatR Migration**: Simple migration path from MediatR 12.5

## Installation

```bash
dotnet add package Medino
dotnet add package Medino.Extensions.DependencyInjection
```

## Quick Start

### 1. Register Medino

```csharp
services.AddMedino(typeof(Program).Assembly);
```

### 2. Define Commands

```csharp
public record CreateUserCommand(string Name, string Email) : ICommand;

public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand>
{
    public async Task HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        // Fire-and-forget command logic
        await _userRepository.CreateAsync(command.Name, command.Email);
    }
}
```

### 3. Define Queries

```csharp
public record GetUserQuery(int Id) : IRequest<UserDto>;

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> HandleAsync(GetUserQuery request, CancellationToken cancellationToken)
    {
        return await _userRepository.GetByIdAsync(request.Id);
    }
}
```

### 4. Send Requests

```csharp
// Send command
await mediator.SendAsync(new CreateUserCommand("John", "john@example.com"));

// Send query
var user = await mediator.SendAsync(new GetUserQuery(123));
```

## Pipeline Behaviors

Add cross-cutting concerns that execute around your handlers:

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}
```

## Events (Notifications)

Publish events to multiple handlers:

```csharp
public record UserCreatedNotification(int UserId, string Email) : INotification;

public class SendWelcomeEmailHandler : INotificationHandler<UserCreatedNotification>
{
    public async Task HandleAsync(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        await _emailService.SendWelcomeEmailAsync(notification.Email);
    }
}

// Publish to all handlers
await mediator.PublishAsync(new UserCreatedNotification(123, "john@example.com"));
```

## Documentation

- [Migration Guide from MediatR](https://github.com/brendankowitz/Medino/blob/main/MIGRATION.md)
- [GitHub Repository](https://github.com/brendankowitz/Medino)

## License

MIT License - see [LICENSE](https://github.com/brendankowitz/Medino/blob/main/LICENSE) for details
