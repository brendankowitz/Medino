# Medino

[![NuGet](https://img.shields.io/nuget/v/Medino.svg)](https://www.nuget.org/packages/Medino/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A lightweight, high-performance in-process mediator for .NET 8+ with support for commands, queries, events, pipeline behaviors, and exception handling.

## Why Medino?

Medino provides a simple, lightweight mediator pattern implementation when you need messaging patterns without the complexity of a full message bus. It's perfect for:

- Implementing CQRS patterns in your application
- Decoupling your application logic from infrastructure concerns
- Adding cross-cutting concerns via pipeline behaviors
- Organizing code using clean architecture principles

## Features

- ✅ **Commands** - Fire-and-forget commands with single handlers
- ✅ **Queries** - Request/response pattern for retrieving data
- ✅ **Notifications** - Publish/subscribe for events with multiple handlers
- ✅ **Pipeline Behaviors** - Add cross-cutting concerns (logging, validation, caching, etc.)
- ✅ **Exception Handlers** - Gracefully handle exceptions at the request level
- ✅ **Exception Actions** - Execute side effects when exceptions occur (logging, telemetry)
- ✅ **Async/await** - Full async support with cancellation tokens
- ✅ **No external dependencies** - Only requires Microsoft.Extensions.DependencyInjection.Abstractions
- ✅ **High performance** - Minimal overhead with handler caching
- ✅ **.NET 8 & .NET 9** - Built for modern .NET

## Installation

```bash
dotnet add package Medino
```

## Quick Start

### 1. Register Medino in your DI container

```csharp
using Medino;

var builder = WebApplication.CreateBuilder(args);

// Register Medino and scan current assembly for handlers
builder.Services.AddMedino();

// Or scan specific assemblies
builder.Services.AddMedino(typeof(Program).Assembly, typeof(MyHandler).Assembly);

// Or use configuration
builder.Services.AddMedino(config => config
    .RegisterServicesFromAssemblyContaining<Program>()
    .RegisterServicesFromAssemblyContaining<MyHandler>());

var app = builder.Build();
```

### 2. Define your requests and handlers

#### Commands (no return value)

```csharp
// Define a command
public record CreateUserCommand(string Name, string Email) : ICommand;

// Define a command handler
public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand>
{
    private readonly IUserRepository _repository;

    public CreateUserCommandHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var user = new User { Name = command.Name, Email = command.Email };
        await _repository.AddAsync(user, cancellationToken);
    }
}
```

#### Queries (with return value)

```csharp
// Define a query
public record GetUserQuery(int UserId) : IRequest<User>;

// Define a query handler
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, User>
{
    private readonly IUserRepository _repository;

    public GetUserQueryHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<User> HandleAsync(GetUserQuery query, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(query.UserId, cancellationToken);
    }
}
```

#### Notifications (multiple handlers)

```csharp
// Define a notification
public record UserCreatedNotification(int UserId, string Email) : INotification;

// Define notification handlers (can have multiple)
public class SendWelcomeEmailHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly IEmailService _emailService;

    public SendWelcomeEmailHandler(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task HandleAsync(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        await _emailService.SendWelcomeEmailAsync(notification.Email, cancellationToken);
    }
}

public class LogUserCreationHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly ILogger<LogUserCreationHandler> _logger;

    public LogUserCreationHandler(ILogger<LogUserCreationHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User {UserId} was created", notification.UserId);
        await Task.CompletedTask;
    }
}
```

### 3. Use the mediator

```csharp
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        // Send a command
        await _mediator.SendAsync(new CreateUserCommand(request.Name, request.Email));

        // Publish a notification
        await _mediator.PublishAsync(new UserCreatedNotification(userId, request.Email));

        return Ok();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        // Send a query
        var user = await _mediator.SendAsync(new GetUserQuery(id));
        return Ok(user);
    }
}
```

## Pipeline Behaviors

Pipeline behaviors allow you to add cross-cutting concerns like logging, validation, caching, and more.

### Using IPipelineBehavior directly

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);

        var response = await next();

        _logger.LogInformation("Handled {RequestName}", typeof(TRequest).Name);

        return response;
    }
}
```

### Simplified Base Classes

Medino provides base classes to make implementing pipeline behaviors easier when you only need before or after logic:

#### PipelineBehaviorBase - For both before and after logic

```csharp
public class TimingBehavior<TRequest, TResponse> : PipelineBehaviorBase<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TimingBehavior<TRequest, TResponse>> _logger;
    private Stopwatch? _stopwatch;

    protected override Task BeforeAsync(TRequest request, CancellationToken cancellationToken)
    {
        _stopwatch = Stopwatch.StartNew();
        return Task.CompletedTask;
    }

    protected override Task AfterAsync(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        _stopwatch?.Stop();
        _logger.LogInformation("Request {RequestName} took {ElapsedMs}ms",
            typeof(TRequest).Name, _stopwatch?.ElapsedMilliseconds);
        return Task.CompletedTask;
    }
}
```

#### BeforePipelineBehavior - For pre-execution logic only

Perfect for validation, logging start, or modifying request context:

```csharp
public class ValidationBehavior<TRequest, TResponse> : BeforePipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    protected override Task BeforeAsync(TRequest request, CancellationToken cancellationToken)
    {
        if (request is IValidatable validatable && !validatable.IsValid())
        {
            throw new ValidationException("Request validation failed");
        }
        return Task.CompletedTask;
    }
}
```

#### AfterPipelineBehavior - For post-execution logic only

Perfect for caching, logging results, or cleanup:

```csharp
public class CachingBehavior<TRequest, TResponse> : AfterPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICache _cache;

    protected override Task AfterAsync(TRequest request, TResponse response, CancellationToken cancellationToken)
    {
        var cacheKey = $"{typeof(TRequest).Name}:{request.GetHashCode()}";
        _cache.Set(cacheKey, response, TimeSpan.FromMinutes(5));
        return Task.CompletedTask;
    }
}
```

### Context-Aware Pipeline Behaviors

Context behaviors allow you to transform requests and enrich the pipeline with metadata. They execute **before** regular pipeline behaviors.

#### When to Use Context Behaviors vs Regular Behaviors

- **Use `IContextPipelineBehavior`** when you need to:
  - Transform or replace the request (e.g., add tenant context, normalize data)
  - Enrich with metadata that other behaviors might need
  - Work with immutable records and need to create modified versions

- **Use `IPipelineBehavior`** when you need to:
  - Observe the request without modifying it
  - Add logging, timing, or monitoring
  - Control execution flow (short-circuit, exception handling)

#### Request Transformation

Use `RequestTransformBehavior` to modify immutable requests:

```csharp
public class TenantEnrichmentBehavior : RequestTransformBehavior<MyRequest, MyResponse>
{
    private readonly ITenantProvider _tenantProvider;

    public TenantEnrichmentBehavior(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    protected override Task<MyRequest> TransformAsync(MyRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Use 'with' expression to create modified record
        return Task.FromResult(request with { TenantId = tenantId });
    }
}
```

#### Request Enrichment with Metadata

Use `RequestEnrichmentBehavior` to add metadata without changing the request:

```csharp
public class CorrelationBehavior : RequestEnrichmentBehavior<MyRequest, MyResponse>
{
    protected override Task EnrichAsync(PipelineContext<MyRequest> context, CancellationToken cancellationToken)
    {
        context.SetMetadata("CorrelationId", Guid.NewGuid().ToString());
        context.SetMetadata("Timestamp", DateTimeOffset.UtcNow);
        context.SetMetadata("UserId", _currentUser.Id);
        return Task.CompletedTask;
    }
}
```

**Note:** Context behaviors must be strongly typed to specific request/response types. For generic cross-cutting concerns that don't need request transformation, use regular `IPipelineBehavior<object, TResponse>` instead.

#### Full Control with IContextPipelineBehavior

Implement the interface directly for complete control:

```csharp
public class SanitizationBehavior : IContextPipelineBehavior<UserRequest, UserResponse>
{
    public async Task<UserResponse> HandleAsync(
        PipelineContext<UserRequest> context,
        RequestHandlerDelegate<UserResponse> next,
        CancellationToken cancellationToken)
    {
        // Sanitize and replace request
        var sanitized = context.Request with
        {
            Email = context.Request.Email.Trim().ToLowerInvariant(),
            Name = context.Request.Name.Trim()
        };

        context.Request = sanitized;
        context.SetMetadata("WasSanitized", true);

        return await next();
    }
}
```

#### Execution Order

When both context and regular behaviors are registered:

1. **Context Behaviors** execute first (transform request)
2. **Regular Pipeline Behaviors** execute second (observe final request)
3. **Handler** executes last (receives final request)

This ensures transformations happen before observation/validation:

```csharp
services.AddSingleton<IContextPipelineBehavior<MyRequest, MyResponse>, TenantEnrichment>();
services.AddSingleton<IPipelineBehavior<object, MyResponse>, ValidationBehavior>();
services.AddSingleton<IPipelineBehavior<object, MyResponse>, LoggingBehavior>();

// Execution order:
// 1. TenantEnrichment (transforms request)
// 2. ValidationBehavior (validates enriched request)
// 3. LoggingBehavior (logs final request)
// 4. Handler (processes final request)
```

## Exception Handling

### Exception Handlers

Handle exceptions and optionally provide a fallback response:

```csharp
public class ValidationExceptionHandler<TRequest, TResponse>
    : IRequestExceptionHandler<TRequest, TResponse, ValidationException>
    where TRequest : notnull
{
    private readonly ILogger<ValidationExceptionHandler<TRequest, TResponse>> _logger;

    public ValidationExceptionHandler(ILogger<ValidationExceptionHandler<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        TRequest request,
        ValidationException exception,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(exception, "Validation failed for {RequestName}", typeof(TRequest).Name);

        // Optionally set a fallback response
        if (typeof(TResponse) == typeof(ValidationResult))
        {
            state.SetHandled((TResponse)(object)new ValidationResult { Errors = exception.Errors });
        }

        return Task.CompletedTask;
    }
}
```

### Exception Actions

Execute side effects when exceptions occur (for logging, telemetry, etc.):

```csharp
public class LogExceptionAction<TRequest> : IRequestExceptionAction<TRequest, Exception>
    where TRequest : notnull
{
    private readonly ILogger<LogExceptionAction<TRequest>> _logger;

    public LogExceptionAction(ILogger<LogExceptionAction<TRequest>> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(TRequest request, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Error handling {RequestName}: {ExceptionMessage}",
            typeof(TRequest).Name, exception.Message);
        return Task.CompletedTask;
    }
}
```

## Migration from MediatR

See [MIGRATION.md](MIGRATION.md) for a detailed guide on migrating from MediatR 12.5 to Medino.

## Performance

Medino is designed for high performance with minimal overhead:

- Handler type caching to avoid repeated reflection
- No unnecessary allocations
- Direct invocation where possible
- Optimized pipeline construction

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.