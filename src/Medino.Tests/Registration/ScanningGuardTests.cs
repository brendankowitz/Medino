using Medino.Extensions.DependencyInjection;
using Medino.Tests.DuplicateHandlers;
using Medino.Tests.OpenGenericHandlers;
using Microsoft.Extensions.DependencyInjection;
using Medino.Tests.Commands;
using System.Reflection;

namespace Medino.Tests.Registration;

public class ScanningGuardTests
{
    private static readonly Assembly DuplicateAssembly = typeof(DuplicatedCommand).Assembly;
    private static readonly Assembly OpenGenericAssembly = typeof(ClosedCommand).Assembly;

    // --- null guards ---

    [Fact]
    public void AddMedino_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>("services", () => services.AddMedino());
        Assert.Throws<ArgumentNullException>("services", () => services.AddMedino(typeof(ScanningGuardTests).Assembly));
        Assert.Throws<ArgumentNullException>("services", () => services.AddMedino(c => c.RegisterServicesFromAssemblyContaining<ScanningGuardTests>()));
    }

    [Fact]
    public void AddMedino_NullAssemblyArray_Throws()
    {
        Assert.Throws<ArgumentNullException>("assemblies", () => new ServiceCollection().AddMedino((Assembly[])null!));
    }

    [Fact]
    public void AddMedino_NullAssemblyElement_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ServiceCollection().AddMedino(typeof(ScanningGuardTests).Assembly, null!));

        Assert.Equal("assemblies", ex.ParamName);
    }

    [Fact]
    public void AddMedino_NullConfiguration_Throws()
    {
        Assert.Throws<ArgumentNullException>("configuration", () =>
            new ServiceCollection().AddMedino((Action<MediatorConfiguration>)null!));
    }

    [Fact]
    public void RegisterServicesFromAssemblies_NullGuards()
    {
        var config = new MediatorConfiguration();

        Assert.Throws<ArgumentNullException>("assemblies", () => config.RegisterServicesFromAssemblies(null!));
        var ex = Assert.Throws<ArgumentException>(() => config.RegisterServicesFromAssemblies(typeof(ScanningGuardTests).Assembly, null!));
        Assert.Equal("assemblies", ex.ParamName);
    }

    [Fact]
    public void RegisterServicesFromAssemblyContaining_NullGuards()
    {
        var config = new MediatorConfiguration();

        Assert.Throws<ArgumentNullException>("types", () => config.RegisterServicesFromAssemblyContaining((Type[])null!));
        var ex = Assert.Throws<ArgumentException>(() => config.RegisterServicesFromAssemblyContaining(typeof(ScanningGuardTests), null!));
        Assert.Equal("types", ex.ParamName);
    }

    // --- duplicate handlers ---

    [Fact]
    public void AddMedino_DuplicateCommandHandlers_ThrowsNamingBothTypes()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddMedino(DuplicateAssembly));

        Assert.Contains(nameof(FirstDuplicatedCommandHandler), ex.Message);
        Assert.Contains(nameof(SecondDuplicatedCommandHandler), ex.Message);
        Assert.Contains("ICommandHandler", ex.Message);
    }

    [Fact]
    public void AddMedino_SameAssemblyTwice_DoesNotThrowAndRegistersOneHandler()
    {
        var assembly = typeof(ScanningGuardTests).Assembly;
        var services = new ServiceCollection();

        services.AddMedino(assembly, assembly);

        Assert.Single(services, d => d.ServiceType == typeof(ICommandHandler<TestCommand>));
    }

    // --- open generics ---

    [Fact]
    public void AddMedino_OpenGenericHandlers_AreSkippedAndProviderValidates()
    {
        var services = new ServiceCollection();

        services.AddMedino(OpenGenericAssembly);

        Assert.DoesNotContain(services, d => d.ImplementationType is { ContainsGenericParameters: true });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        Assert.IsType<ClosedCommandHandler>(provider.GetRequiredService<ICommandHandler<ClosedCommand>>());
    }
}
