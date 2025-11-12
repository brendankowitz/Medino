using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;
using System.Reflection;

namespace Medino.Tests.Registration;

public class RegistrationTests
{
    [Fact]
    public void AddMedino_WithAssembly_ShouldRegisterMediator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMedino(typeof(RegistrationTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator = provider.GetService<IMediator>();
        Assert.NotNull(mediator);
    }

    [Fact]
    public void AddMedino_WithConfiguration_ShouldRegisterMediator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMedino(config => config.RegisterServicesFromAssemblyContaining<RegistrationTests>());
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator = provider.GetService<IMediator>();
        Assert.NotNull(mediator);
    }

    [Fact]
    public void AddMedino_ShouldRegisterCommandHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMedino(typeof(RegistrationTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Act
        var handler = provider.GetService<ICommandHandler<TestRegistrationCommand>>();

        // Assert
        Assert.NotNull(handler);
        Assert.IsType<TestRegistrationCommandHandler>(handler);
    }

    [Fact]
    public void AddMedino_ShouldRegisterRequestHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMedino(typeof(RegistrationTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Act
        var handler = provider.GetService<IRequestHandler<TestRegistrationQuery, string>>();

        // Assert
        Assert.NotNull(handler);
        Assert.IsType<TestRegistrationQueryHandler>(handler);
    }

    [Fact]
    public void AddMedino_ShouldRegisterNotificationHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMedino(typeof(RegistrationTests).Assembly);
        var provider = services.BuildServiceProvider();

        // Act
        var handlers = provider.GetServices<INotificationHandler<TestRegistrationNotification>>().ToList();

        // Assert
        Assert.NotEmpty(handlers);
        Assert.True(handlers.Count >= 1);
    }

    [Fact]
    public void AddMedino_WithEmptyAssemblyArray_ShouldThrowException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => services.AddMedino(Array.Empty<Assembly>()));
    }

    [Fact]
    public void AddMedino_WithConfiguration_WithNoAssemblies_ShouldThrowException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => services.AddMedino(config => { }));
    }
}

// Test types for registration
public record TestRegistrationCommand : ICommand;

public class TestRegistrationCommandHandler : ICommandHandler<TestRegistrationCommand>
{
    public Task HandleAsync(TestRegistrationCommand command, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public record TestRegistrationQuery : IRequest<string>;

public class TestRegistrationQueryHandler : IRequestHandler<TestRegistrationQuery, string>
{
    public Task<string> HandleAsync(TestRegistrationQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult("Test");
    }
}

public record TestRegistrationNotification : INotification;

public class TestRegistrationNotificationHandler : INotificationHandler<TestRegistrationNotification>
{
    public Task HandleAsync(TestRegistrationNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}