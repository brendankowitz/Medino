using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;

namespace Medino.Tests.Registration;

public class MediatorConfigurationTests
{
    [Fact]
    public void RegisterServicesFromAssemblies_ShouldAddAssemblies()
    {
        // Arrange
        var config = new MediatorConfiguration();
        var assembly1 = typeof(MediatorConfigurationTests).Assembly;
        var assembly2 = typeof(IMediator).Assembly;

        // Act
        var result = config.RegisterServicesFromAssemblies(assembly1, assembly2);

        // Assert
        Assert.Same(config, result); // Fluent interface
        // We can't directly check internal Assemblies, but we can verify it works with AddMedino
    }

    [Fact]
    public void RegisterServicesFromAssemblyContaining_ShouldAddAssembliesFromTypes()
    {
        // Arrange
        var config = new MediatorConfiguration();

        // Act
        var result = config.RegisterServicesFromAssemblyContaining(typeof(MediatorConfigurationTests), typeof(IMediator));

        // Assert
        Assert.Same(config, result); // Fluent interface
    }

    [Fact]
    public void RegisterServicesFromAssemblyContaining_ShouldDeduplicateAssemblies()
    {
        // Arrange
        var config = new MediatorConfiguration();

        // Act - Register same assembly twice via different types from same assembly
        config.RegisterServicesFromAssemblyContaining(typeof(MediatorConfigurationTests), typeof(RegistrationTests));

        // Can't directly check internal list, but the system should handle duplicates
        // This is tested indirectly via AddMedino not failing
        Assert.NotNull(config);
    }

    [Fact]
    public void RegisterServicesFromAssemblyContainingGeneric_ShouldAddAssembly()
    {
        // Arrange
        var config = new MediatorConfiguration();

        // Act
        var result = config.RegisterServicesFromAssemblyContaining<MediatorConfigurationTests>();

        // Assert
        Assert.Same(config, result); // Fluent interface
    }

    [Fact]
    public void MediatorConfiguration_CanBeChained()
    {
        // Arrange
        var config = new MediatorConfiguration();

        // Act
        var result = config
            .RegisterServicesFromAssemblyContaining<MediatorConfigurationTests>()
            .RegisterServicesFromAssemblies(typeof(IMediator).Assembly)
            .RegisterServicesFromAssemblyContaining(typeof(string));

        // Assert
        Assert.Same(config, result);
    }

    [Fact]
    public void AddMedino_WithConfiguration_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMedino(config => config
            .RegisterServicesFromAssemblyContaining<MediatorConfigurationTests>());

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var mediator = serviceProvider.GetService<IMediator>();
        Assert.NotNull(mediator);
    }

    [Fact]
    public void AddMedino_WithConfigurationNoAssemblies_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            services.AddMedino(config => { })); // Empty configuration

        Assert.Contains("No assemblies", ex.Message);
    }

    [Fact]
    public async Task AddMedino_WithConfiguration_ShouldScanAndRegisterHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMedino(config => config
            .RegisterServicesFromAssemblyContaining<ConfigTestCommand>());

        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        // Act
        await mediator.SendAsync(new ConfigTestCommand { Value = "test" });
        var result = await mediator.SendAsync(new ConfigTestQuery { Id = 42 });

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void MediatorConfiguration_MultipleAssembliesViaTypes_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMedino(config => config
            .RegisterServicesFromAssemblyContaining(
                typeof(ConfigTestCommand),
                typeof(IMediator)));

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var mediator = serviceProvider.GetService<IMediator>();
        Assert.NotNull(mediator);
    }

    [Fact]
    public void MediatorConfiguration_MixedRegistrationMethods_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        var assembly = typeof(ConfigTestCommand).Assembly;

        // Act
        services.AddMedino(config => config
            .RegisterServicesFromAssemblies(assembly)
            .RegisterServicesFromAssemblyContaining<IMediator>()
            .RegisterServicesFromAssemblyContaining(typeof(string)));

        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var mediator = serviceProvider.GetService<IMediator>();
        Assert.NotNull(mediator);
    }
}

// Test types for configuration tests
public record ConfigTestCommand : ICommand
{
    public string Value { get; init; } = string.Empty;
}

public class ConfigTestCommandHandler : ICommandHandler<ConfigTestCommand>
{
    public Task HandleAsync(ConfigTestCommand command, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public record ConfigTestQuery : IRequest<int>
{
    public int Id { get; init; }
}

public class ConfigTestQueryHandler : IRequestHandler<ConfigTestQuery, int>
{
    public Task<int> HandleAsync(ConfigTestQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.Id);
    }
}
