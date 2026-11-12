# Medino.Extensions.DependencyInjection

Microsoft.Extensions.DependencyInjection integration for Medino mediator.

Supports .NET 10 and .NET 11 (`net10.0` and `net11.0`); .NET 10 is the minimum supported runtime.

## Installation

```bash
dotnet add package Medino.Extensions.DependencyInjection
```

## Quick Start

### Basic Registration

```csharp
using Medino.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register Medino and scan current assembly for handlers
services.AddMedino(typeof(Program).Assembly);

var serviceProvider = services.BuildServiceProvider();
var mediator = serviceProvider.GetRequiredService<IMediator>();
```

The parameterless `services.AddMedino()` overload scans the calling assembly by using `Assembly.GetCallingAssembly()`. On .NET 11, it may throw if the application has disabled stack-trace support. In that case, use the existing explicit-assembly overload shown above, for example `services.AddMedino(typeof(Program).Assembly)`. This is a runtime limitation of that overload, not a change to Medino's registration behavior.

### Fluent Configuration

```csharp
services.AddMedino(config => config
    .RegisterServicesFromAssemblyContaining<Startup>()
    .RegisterServicesFromAssemblyContaining<MyCommand>());
```

### What Gets Registered

The `AddMedino` extension automatically scans assemblies and registers:

- **Command handlers** (`ICommandHandler<TCommand>`)
- **Request handlers** (`IRequestHandler<TRequest, TResponse>`)
- **Notification handlers** (`INotificationHandler<TNotification>`)
- **Pipeline behaviors** (`IPipelineBehavior<TRequest, TResponse>`)
- **Context pipeline behaviors** (`IContextPipelineBehavior<TRequest, TResponse>`)
- **Exception handlers** (`IRequestExceptionHandler<TRequest, TResponse, TException>`)
- **Exception actions** (`IRequestExceptionAction<TRequest, TException>`)

All services are registered as **transient** by default.

## Configuration Options

### Register from Multiple Assemblies

```csharp
services.AddMedino(
    typeof(CommandsAssembly).Assembly,
    typeof(QueriesAssembly).Assembly,
    typeof(HandlersAssembly).Assembly);
```

### Register by Assembly Containing Type

```csharp
services.AddMedino(config => config
    .RegisterServicesFromAssemblyContaining(
        typeof(CreateUserCommand),
        typeof(GetUserQuery)));
```

### Register by Generic Type

```csharp
services.AddMedino(config => config
    .RegisterServicesFromAssemblyContaining<CreateUserCommand>()
    .RegisterServicesFromAssemblyContaining<GetUserQuery>());
```

## Example Usage

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register Medino
builder.Services.AddMedino(typeof(Program).Assembly);

var app = builder.Build();

// Use in minimal API
app.MapPost("/users", async (CreateUserCommand command, IMediator mediator) =>
{
    await mediator.SendAsync(command);
    return Results.Created($"/users/{command.UserId}", null);
});

app.Run();
```

## ASP.NET Core Integration

```csharp
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserCommand command)
    {
        await _mediator.SendAsync(command);
        return Created($"/users/{command.UserId}", null);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var user = await _mediator.SendAsync(new GetUserQuery { Id = id });
        return Ok(user);
    }
}
```

## Requirements

- .NET 10 or .NET 11 (minimum supported runtime: .NET 10)
- For the `net10.0` asset: `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.15 or later
- For the `net11.0` asset: .NET 11 supplies `Microsoft.Extensions.DependencyInjection.Abstractions` via the shared framework

## Documentation

For more information about Medino and its features, see the [main package](https://www.nuget.org/packages/Medino).

## License

MIT License - see [LICENSE](https://github.com/brendankowitz/Medino/blob/main/LICENSE) for details
