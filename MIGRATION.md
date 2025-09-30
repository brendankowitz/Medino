# Migration Guide from MediatR 12.5 to Medino

This guide will help you migrate from MediatR 12.5 to Medino. The migration is straightforward as Medino provides similar features with a simplified API.

## Key Differences

### Namespace
- **MediatR**: `MediatR`
- **Medino**: `Medino`

### Interface Names

| MediatR 12.5 | Medino | Notes |
|-------------|---------|-------|
| `IRequest<TResponse>` | `IRequest<TResponse>` | ✅ Same |
| `IRequestHandler<TRequest, TResponse>` | `IRequestHandler<TRequest, TResponse>` | Method renamed to `HandleAsync` |
| `INotification` | `INotification` | ✅ Same |
| `INotificationHandler<TNotification>` | `INotificationHandler<TNotification>` | Method renamed to `HandleAsync` |
| `IBaseRequest` | - | Not needed in Medino |
| `Unit` | Use `ICommand` for void requests | Medino has explicit command support |
| `ISender` | `IMediator` | Simplified interface |
| `IPublisher` | `IMediator` | Unified interface |
| `IMediator` | `IMediator` | Combined sender and publisher |

## Step-by-Step Migration

### 1. Update Package References

Remove MediatR:
```bash
dotnet remove package MediatR
dotnet remove package MediatR.Extensions.Microsoft.DependencyInjection
```

Add Medino:
```bash
dotnet add package Medino
```

### 2. Update Namespaces

**Before (MediatR):**
```csharp
using MediatR;
```

**After (Medino):**
```csharp
using Medino;
```

### 3. Update Registration

**Before (MediatR):**
```csharp
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

**After (Medino):**
```csharp
builder.Services.AddMedino(typeof(Program).Assembly);

// Or with configuration
builder.Services.AddMedino(config => config
    .RegisterServicesFromAssemblyContaining<Program>());
```

### 4. Migrate Commands (Unit Requests)

**Before (MediatR):**
```csharp
public class CreateUserCommand : IRequest<Unit>
{
    public string Name { get; set; }
    public string Email { get; set; }
}

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Unit>
{
    public async Task<Unit> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Do work
        return Unit.Value;
    }
}

// Usage
await _mediator.Send(new CreateUserCommand());
```

**After (Medino):**
```csharp
public record CreateUserCommand(string Name, string Email) : ICommand;

public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand>
{
    public async Task HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        // Do work
    }
}

// Usage
await _mediator.SendAsync(new CreateUserCommand("John", "john@example.com"));
```

### 5. Migrate Queries (Value Requests)

**Before (MediatR):**
```csharp
public class GetUserQuery : IRequest<User>
{
    public int UserId { get; set; }
}

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, User>
{
    public async Task<User> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        // Fetch user
        return user;
    }
}

// Usage
var user = await _mediator.Send(new GetUserQuery { UserId = 1 });
```

**After (Medino):**
```csharp
public record GetUserQuery(int UserId) : IRequest<User>;

public class GetUserQueryHandler : IRequestHandler<GetUserQuery, User>
{
    public async Task<User> HandleAsync(GetUserQuery query, CancellationToken cancellationToken)
    {
        // Fetch user
        return user;
    }
}

// Usage
var user = await _mediator.SendAsync(new GetUserQuery(1));
```

### 6. Migrate Notifications

**Before (MediatR):**
```csharp
public class UserCreatedNotification : INotification
{
    public int UserId { get; set; }
}

public class UserCreatedNotificationHandler : INotificationHandler<UserCreatedNotification>
{
    public async Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        // Handle notification
    }
}

// Usage
await _mediator.Publish(new UserCreatedNotification { UserId = 1 });
```

**After (Medino):**
```csharp
public record UserCreatedNotification(int UserId) : INotification;

public class UserCreatedNotificationHandler : INotificationHandler<UserCreatedNotification>
{
    public async Task HandleAsync(UserCreatedNotification notification, CancellationToken cancellationToken)
    {
        // Handle notification
    }
}

// Usage
await _mediator.PublishAsync(new UserCreatedNotification(1));
```

### 7. Migrate Pipeline Behaviors

**Before (MediatR):**
```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Before
        var response = await next();
        // After
        return response;
    }
}
```

**After (Medino):**
```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Before
        var response = await next();
        // After
        return response;
    }
}
```

**Changes:** Method renamed from `Handle` to `HandleAsync`.

### 8. Migrate Exception Handlers

**Before (MediatR 12.5):**
```csharp
public class ValidationExceptionHandler<TRequest, TResponse, TException>
    : IRequestExceptionHandler<TRequest, TResponse, TException>
    where TRequest : notnull
    where TException : Exception
{
    public Task Handle(
        TRequest request,
        TException exception,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken)
    {
        state.SetHandled(default);
        return Task.CompletedTask;
    }
}
```

**After (Medino):**
```csharp
public class ValidationExceptionHandler<TRequest, TResponse, TException>
    : IRequestExceptionHandler<TRequest, TResponse, TException>
    where TRequest : notnull
    where TException : Exception
{
    public Task HandleAsync(
        TRequest request,
        TException exception,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken)
    {
        state.SetHandled(default);
        return Task.CompletedTask;
    }
}
```

**Changes:** Method renamed from `Handle` to `HandleAsync`.

### 9. Migrate Exception Actions

MediatR 12.5 and Medino both support `IRequestExceptionAction`. The migration is simple:

**Before (MediatR):**
```csharp
public class LogExceptionAction<TRequest, TException> : IRequestExceptionAction<TRequest, TException>
    where TRequest : notnull
    where TException : Exception
{
    public Task Execute(TRequest request, TException exception, CancellationToken cancellationToken)
    {
        // Log exception
        return Task.CompletedTask;
    }
}
```

**After (Medino):**
```csharp
public class LogExceptionAction<TRequest, TException> : IRequestExceptionAction<TRequest, TException>
    where TRequest : notnull
    where TException : Exception
{
    public Task ExecuteAsync(TRequest request, TException exception, CancellationToken cancellationToken)
    {
        // Log exception
        return Task.CompletedTask;
    }
}
```

**Changes:** Method renamed from `Execute` to `ExecuteAsync`.

## Method Name Changes Summary

All handler methods have been renamed to follow async naming conventions:

| MediatR Method | Medino Method |
|----------------|----------------|
| `Handle()` | `HandleAsync()` |
| `Execute()` | `ExecuteAsync()` |

This makes the async nature of the methods explicit and follows modern .NET conventions.

## Additional Features in Medino

### 1. Explicit Command Support

Medino introduces `ICommand` and `ICommandHandler<TCommand>` for fire-and-forget operations, eliminating the need for `Unit`:

```csharp
// Instead of IRequest<Unit>
public record MyCommand : ICommand;

public class MyCommandHandler : ICommandHandler<MyCommand>
{
    public async Task HandleAsync(MyCommand command, CancellationToken cancellationToken)
    {
        // No need to return Unit.Value
    }
}
```

### 2. Unified Mediator Interface

Medino combines `ISender` and `IPublisher` into a single `IMediator` interface, simplifying dependency injection:

```csharp
// One interface for everything
public class MyService
{
    private readonly IMediator _mediator;

    public MyService(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task DoWork()
    {
        await _mediator.SendAsync(new MyCommand());
        await _mediator.PublishAsync(new MyNotification());
        var result = await _mediator.SendAsync(new MyQuery());
    }
}
```

## Breaking Changes

1. **Method names**: All `Handle()` methods are now `HandleAsync()`
2. **ICommand**: Use `ICommand` instead of `IRequest<Unit>` for void operations
3. **Registration**: Different registration API (but simpler)
4. **No StreamBehavior**: Medino doesn't support streaming (yet)

## Gradual Migration Strategy

If you have a large codebase, consider this approach:

1. **Start with a single assembly**: Migrate one bounded context or module at a time
2. **Create adapter classes**: If needed, create adapters that implement both MediatR and Medino interfaces during transition
3. **Update tests incrementally**: Update unit tests as you migrate handlers
4. **Use feature flags**: If possible, use feature flags to toggle between implementations during migration

## Common Issues and Solutions

### Issue: Missing Unit type

**Solution:** Replace `IRequest<Unit>` with `ICommand` and remove `Unit.Value` returns.

### Issue: ISender not found

**Solution:** Replace `ISender` with `IMediator`. The `SendAsync` method works the same way.

### Issue: Handle method not found

**Solution:** Rename all `Handle()` methods to `HandleAsync()`.

### Issue: Registration not working

**Solution:** Ensure you're scanning the correct assemblies with `AddMedino()`.

## Testing Framework

Medino uses **xUnit** for its test suite. If you're migrating tests from a project that used NUnit alongside MediatR, here are the key differences:

### NUnit to xUnit Migration

| NUnit | xUnit | Notes |
|-------|-------|-------|
| `[TestFixture]` | Remove attribute | xUnit auto-discovers test classes |
| `[Test]` | `[Fact]` | For tests without parameters |
| `[TestCase]` | `[Theory]` + `[InlineData]` | For parameterized tests |
| `[SetUp]` | Constructor | Per-test setup |
| `[TearDown]` | `IDisposable.Dispose()` | Per-test cleanup |
| `[OneTimeSetUp]` | `IClassFixture<T>` | Shared context across tests |
| `[OneTimeTearDown]` | `IClassFixture<T>` with `IDisposable` | Shared cleanup |
| `Assert.That(x, Is.EqualTo(y))` | `Assert.Equal(y, x)` | Note parameter order! |
| `Assert.That(x, Is.Not.Null)` | `Assert.NotNull(x)` | |
| `Assert.That(x, Is.True)` | `Assert.True(x)` | |
| `Assert.ThrowsAsync<T>()` | `await Assert.ThrowsAsync<T>()` | Must await |
| `Assert.DoesNotThrowAsync()` | Just call the method | xUnit has no equivalent |

### Example xUnit Test

```csharp
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;

public class CommandTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public CommandTests()
    {
        var services = new ServiceCollection();
        services.AddMedino(typeof(CommandTests).Assembly);
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task Command_ShouldExecute()
    {
        // Arrange
        var command = new TestCommand();

        // Act
        await _mediator.SendAsync(command);

        // Assert
        Assert.True(command.WasHandled);
    }
}
```

### Shared Test Context with IClassFixture

For tests that need shared setup across multiple test classes:

```csharp
// Define the fixture
public class MediatorFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public IMediator Mediator { get; }

    public MediatorFixture()
    {
        var services = new ServiceCollection();
        services.AddMedino(typeof(MediatorFixture).Assembly);
        ServiceProvider = services.BuildServiceProvider();
        Mediator = ServiceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

// Define the collection
[CollectionDefinition("Mediator Tests")]
public class MediatorCollection : ICollectionFixture<MediatorFixture>
{
}

// Use in test classes
[Collection("Mediator Tests")]
public class CommandTests
{
    private readonly IMediator _mediator;

    public CommandTests(MediatorFixture fixture)
    {
        _mediator = fixture.Mediator;
    }

    [Fact]
    public async Task Command_ShouldExecute()
    {
        await _mediator.SendAsync(new TestCommand());
    }
}
```

## Need Help?

If you encounter issues during migration:

1. Check the [README.md](README.md) for usage examples
2. Review the unit tests in the Medino repository
3. Open an issue on the GitHub repository

## Benefits of Migrating

- ✅ **Simpler API**: Fewer interfaces and concepts to learn
- ✅ **Better performance**: Optimized handler resolution and caching
- ✅ **Explicit async**: All methods clearly indicate async behavior
- ✅ **Modern .NET**: Built for .NET 8+ with latest language features
- ✅ **No commercial licensing concerns**: BSD-3-Clause license

## Comparison Table

| Feature | MediatR 12.5 | Medino |
|---------|--------------|---------|
| Commands | IRequest&lt;Unit&gt; | ICommand |
| Queries | IRequest&lt;T&gt; | IRequest&lt;T&gt; |
| Notifications | INotification | INotification |
| Pipeline Behaviors | ✅ | ✅ |
| Context Pipeline Behaviors | ❌ | ✅ (request transformation) |
| Exception Handlers | ✅ | ✅ |
| Exception Actions | ✅ | ✅ (with exception translation) |
| Streaming | ✅ | ❌ (not yet) |
| Pre/Post Processors | ✅ | Use Pipeline Behaviors |
| Base Behavior Classes | ❌ | ✅ (Before/After/Base) |
| Target Framework | .NET Standard 2.0+ | .NET 8+ |
| Test Framework | Any (typically NUnit) | xUnit |
| License | Moving to commercial | MIT |

---

Happy migrating! 🚀