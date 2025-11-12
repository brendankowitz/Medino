# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Medino** is a lightweight, high-performance in-process mediator for .NET 8+ that implements the mediator pattern for CQRS, commands, queries, events, and pipeline behaviors. It provides a simplified migration path from MediatR with explicit async naming conventions and no external dependencies beyond Microsoft.Extensions.DependencyInjection.Abstractions.

## Build & Test Commands

### Building
```bash
# Build the entire solution
dotnet build src/Medino.slnx

# Build specific project
dotnet build src/Medino/Medino.csproj
dotnet build src/Medino.Extensions.DependencyInjection/Medino.Extensions.DependencyInjection.csproj

# Clean build
dotnet clean src/Medino.slnx
```

### Testing
```bash
# Run all tests
dotnet test src/Medino.Tests/Medino.Tests.csproj

# Run tests with verbosity
dotnet test src/Medino.Tests/Medino.Tests.csproj --verbosity normal

# Run specific test
dotnet test src/Medino.Tests/Medino.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

### Packaging
```bash
# Pack the core library
dotnet pack src/Medino/Medino.csproj

# Pack the DI extensions
dotnet pack src/Medino.Extensions.DependencyInjection/Medino.Extensions.DependencyInjection.csproj
```

## Architecture

### Core Components

1. **Mediator Pattern Implementation** (`src/Medino/Mediator.cs`)
   - Central `Mediator` class implements `IMediator` interface
   - Uses `IMediatorServiceProvider` abstraction for service resolution to avoid tight coupling to DI frameworks
   - Supports three messaging patterns: Commands (fire-and-forget), Queries (request/response), and Notifications (pub/sub)
   - Pipeline construction uses reflection to invoke handlers dynamically, supporting behaviors and exception handling

2. **Service Provider Abstraction** (`src/Medino/IMediatorServiceProvider.cs`)
   - Decouples mediator from specific DI implementations
   - `GetService<T>()` returns single service instance (generic, avoids boxing)
   - `GetServices<T>()` returns all matching service instances for notifications (multiple handlers)
   - Uses generic methods to avoid boxing and improve type safety

3. **Request Types**
   - `ICommand`: Fire-and-forget operations (no return value)
   - `IRequest<TResponse>`: Queries that return a response
   - `INotification`: Events that can have multiple handlers

4. **Handler Types**
   - `ICommandHandler<TCommand>`: Single handler for commands
   - `IRequestHandler<TRequest, TResponse>`: Single handler for queries
   - `INotificationHandler<TNotification>`: Multiple handlers allowed for notifications

5. **Pipeline Behaviors**
   - `IPipelineBehavior<TRequest, TResponse>`: Regular behaviors for cross-cutting concerns (logging, validation, etc.)
   - `IContextPipelineBehavior<TRequest, TResponse>`: Context behaviors that execute **first** and can transform requests
   - Base classes: `PipelineBehaviorBase`, `BeforePipelineBehavior`, `AfterPipelineBehavior`
   - Context base classes: `RequestTransformBehavior`, `RequestEnrichmentBehavior`

6. **Exception Handling**
   - `IRequestExceptionHandler<TRequest, TResponse, TException>`: Handles exceptions and optionally provides fallback responses
   - `IRequestExceptionAction<TRequest, TException>`: Executes side effects (logging, telemetry) and can translate exceptions

### Pipeline Execution Order

When a request is sent through the mediator:
1. **Context Behaviors** execute first (can transform request via `PipelineContext<T>`)
2. **Regular Pipeline Behaviors** execute second (observe final request)
3. **Handler** executes last (processes final request)
4. **Exception Handlers** catch and optionally provide fallback responses
5. **Exception Actions** execute for side effects (logging) and can translate exceptions

### DI Integration (`src/Medino.Extensions.DependencyInjection/`)

- `ServiceCollectionExtensions` provides `AddMedino()` extension methods
- Scans assemblies for handlers, behaviors, and exception handlers using reflection
- Registers all services as transient by default
- `MediatorServiceProviderAdapter` bridges `IServiceProvider` to `IMediatorServiceProvider`

## Key Design Patterns

### Handler Resolution Strategy
The mediator uses reflection to:
1. Build handler type from request type (e.g., `ICommandHandler<CreateUserCommand>`)
2. Resolve handler from service provider at runtime
3. Invoke `HandleAsync()` method dynamically using `MethodInfo.Invoke()`
4. Unwrap `TargetInvocationException` to preserve original stack traces

### Performance Considerations
- Service provider uses generic methods to avoid boxing/unboxing
- Pipeline construction happens per-request but behaviors are resolved once from DI container
- Handler resolution uses reflection for dynamic type construction (required since request types are only known at runtime)
- No handler caching needed - DI container handles instance resolution efficiently

## Project Structure

```
src/
├── Medino/                          # Core mediator library
│   ├── IMediator.cs                 # Main mediator interface
│   ├── Mediator.cs                  # Mediator implementation
│   ├── ICommand.cs                  # Command interface
│   ├── ICommandHandler.cs           # Command handler interface
│   ├── IRequest.cs                  # Query interface
│   ├── IRequestHandler.cs           # Query handler interface
│   ├── INotification.cs             # Event interface
│   ├── INotificationHandler.cs      # Event handler interface
│   ├── IPipelineBehavior.cs         # Regular behavior interface
│   ├── IContextPipelineBehavior.cs  # Context behavior interface
│   ├── PipelineBehaviorBase.cs      # Base classes for behaviors
│   ├── PipelineContext.cs           # Context for request transformation
│   ├── IRequestExceptionHandler.cs  # Exception handler interface
│   ├── IRequestExceptionAction.cs   # Exception action interface
│   └── IMediatorServiceProvider.cs  # Service provider abstraction
├── Medino.Extensions.DependencyInjection/
│   ├── ServiceCollectionExtensions.cs      # AddMedino() registration
│   └── MediatorServiceProviderAdapter.cs   # IServiceProvider adapter
├── Medino.Tests/                    # xUnit test project
│   ├── Commands/                    # Command handler tests
│   ├── Requests/                    # Query handler tests
│   ├── Events/                      # Notification handler tests
│   ├── PipelineBehaviors/           # Behavior tests
│   ├── ExceptionHandling/           # Exception handler tests
│   └── Registration/                # DI registration tests
└── Medino.Package/                  # Legacy packaging project
```

## Testing Framework

The project uses **xUnit** as its testing framework:
- Test classes don't need `[TestFixture]` attribute (auto-discovered)
- Use `[Fact]` for simple tests, `[Theory]` with `[InlineData]` for parameterized tests
- Constructor runs before each test (like `[SetUp]`)
- Implement `IDisposable.Dispose()` for cleanup (like `[TearDown]`)
- Use `IClassFixture<T>` for shared context across tests in a class
- Use `[Collection]` with `ICollectionFixture<T>` for shared context across test classes

## GitHub Actions Workflows

The repository includes three GitHub Actions workflows:

### 1. Reusable Build and Test Workflow (`build-and-test.yml`)
- Shared workflow called by both PR and CI workflows
- Sets up .NET SDK (configurable version, defaults to 9.0.x)
- Restores dependencies, builds in Release configuration (builds for ALL target frameworks: net8.0 and net9.0)
- Runs tests with trx logger and uploads test results as artifacts
- Tests run on .NET 9 (the latest), but build validates both target frameworks

### 2. PR Validation Workflow (`pr.yml`)
- Triggers on pull requests to `master` or `main` branches
- Only runs when changes are made to `src/**` or workflow files
- Uses the shared build-and-test workflow

### 3. CI Build Workflow (`ci.yml`)
- Triggers on pushes to `master` or `main` branches
- Can be manually triggered via `workflow_dispatch`
- Uses the shared build-and-test workflow
- Includes commented-out package publishing job that when enabled will:
  - Use `dotnet pack` which automatically builds for ALL target frameworks (net8.0 and net9.0)
  - Create NuGet packages containing both framework versions
  - Push packages to NuGet.org (requires `NUGET_API_KEY` secret)

## Development Guidelines

### Adding New Features
1. Define interfaces in `src/Medino/` (core abstractions)
2. Implement in `Mediator.cs` if modifying core behavior
3. Add registration logic in `ServiceCollectionExtensions.cs` if new handler types
4. Write tests in `src/Medino.Tests/` following existing structure
5. Update README.md with usage examples
6. Consider updating MIGRATION.md if affects MediatR migration path

### Naming Conventions
- All async methods use `*Async` suffix (e.g., `HandleAsync`, `SendAsync`, `PublishAsync`)
- Use records for commands, queries, and notifications (immutable by default)
- Handler classes named `<RequestType>Handler` (e.g., `CreateUserCommandHandler`)

### Target Frameworks
- Core library targets: `net8.0` and `net9.0`
- Tests target: `net8.0`
- Uses latest C# language version with nullable reference types enabled

## Migration from MediatR

This project provides a migration path from MediatR 12.5. Key differences:
- Method names: `Handle()` → `HandleAsync()`
- Commands: `IRequest<Unit>` → `ICommand` (no need to return `Unit.Value`)
- Unified interface: `ISender` and `IPublisher` → `IMediator`
- Explicit async: All methods clearly indicate async behavior

See MIGRATION.md for detailed migration guide.

## Package Information

- **Package ID**: `Medino`
- **Version**: 2.0.0
- **License**: MIT
- **Dependencies**: Only `Microsoft.Extensions.DependencyInjection.Abstractions` for core library
- **Separate package**: `Medino.Extensions.DependencyInjection` for DI integration