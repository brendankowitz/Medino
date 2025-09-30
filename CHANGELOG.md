# Changelog

## Version 2.0.0 - Complete Modernization (2025)

### Breaking Changes

This is a complete rewrite of Medino, modernizing it for .NET 8+ and removing legacy dependencies.

#### Target Framework
- **Removed**: .NET 4.0, .NET 4.5 support
- **Added**: .NET 8.0 and .NET 9.0 support

#### Dependencies
- **Removed**: Autofac dependency - now uses Microsoft.Extensions.DependencyInjection
- **Removed**: Nimbus references - all interfaces renamed to "Mediator" pattern
- **Added**: Microsoft.Extensions.DependencyInjection.Abstractions (only external dependency)

#### API Changes

##### Interfaces Renamed
- `IBus` → `IMediator`
- `IBusCommand` → `ICommand`
- `IBusRequest<TRequest, TResponse>` → `IRequest<TResponse>`
- `IBusEvent` → `INotification`
- `IBusResponse` → Removed (no longer needed)
- `IHandleCommand<T>` → `ICommandHandler<T>`
- `IHandleRequest<TRequest, TResponse>` → `IRequestHandler<TRequest, TResponse>`
- `IHandleMulticastEvent<T>` → `INotificationHandler<T>`

##### Method Names
All handler methods renamed from `Handle()` to `HandleAsync()` to be explicit about async behavior:
- `Handle(TCommand command)` → `HandleAsync(TCommand command, CancellationToken cancellationToken)`
- `Handle(TRequest request)` → `HandleAsync(TRequest request, CancellationToken cancellationToken)`

##### Mediator Methods
- `Send()` → `SendAsync()`
- `Request()` → `SendAsync()` (unified with commands)
- `Publish()` → `PublishAsync()`

##### Registration
Old (Autofac):
```csharp
builder.RegisterType<InProcessMediator>()
    .AsImplementedInterfaces()
    .AutoActivate()
    .OnActivated(c => Mediator.SetInstance(c.Instance))
    .SingleInstance();

builder.RegisterAssemblyTypes(ThisAssembly)
    .Where(t => t.IsClosedTypeOf(typeof(IHandleCommand<>)))
    .AsImplementedInterfaces()
    .InstancePerLifetimeScope();
```

New (Microsoft.Extensions.DependencyInjection):
```csharp
services.AddMedino(typeof(Program).Assembly);
```

### New Features

#### Pipeline Behaviors
Added support for cross-cutting concerns:
```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Before handler
        var response = await next();
        // After handler
        return response;
    }
}
```

#### Exception Handlers
Handle exceptions gracefully with optional fallback responses:
```csharp
public class MyExceptionHandler<TRequest, TResponse>
    : IRequestExceptionHandler<TRequest, TResponse, ValidationException>
    where TRequest : notnull
{
    public Task HandleAsync(
        TRequest request,
        ValidationException exception,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken)
    {
        state.SetHandled(defaultResponse);
        return Task.CompletedTask;
    }
}
```

#### Exception Actions
Execute side effects when exceptions occur:
```csharp
public class LogExceptionAction<TRequest> : IRequestExceptionAction<TRequest, Exception>
    where TRequest : notnull
{
    public Task ExecuteAsync(TRequest request, Exception exception, CancellationToken cancellationToken)
    {
        // Log the exception
        return Task.CompletedTask;
    }
}
```

### Improvements

- **Performance**: Handler type caching to avoid repeated reflection
- **Modern C#**: Uses records, nullable reference types, global usings
- **Better API**: Unified `IMediator` interface instead of separate `ISender`/`IPublisher`
- **Explicit async**: All methods clearly indicate async behavior
- **Documentation**: Comprehensive README and migration guide
- **License**: MIT License (open source)

### Migration

See [MIGRATION.md](MIGRATION.md) for a detailed guide on migrating from:
- Medino 1.x
- MediatR 12.5

### Removed

- `InProcessMediator` class (replaced by `Mediator`)
- `InProcessResolvedMediator` class
- `InProcessScopedMediator` class
- Static `Mediator.SetInstance()` pattern
- Compat40 folder with legacy interfaces
- .NET 4.0 and .NET 4.5 projects
- Autofac integration
- Nimbus compatibility layer

---

## Version 1.x - Legacy

Previous versions supported .NET 4.0+ with Autofac and Nimbus compatibility.