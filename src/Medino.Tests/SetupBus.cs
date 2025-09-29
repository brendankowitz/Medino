using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;

namespace Medino.Tests;

public class GlobalSetup : IDisposable
{
    public IServiceProvider ServiceProvider { get; private set; }
    public IMediator Mediator { get; private set; }

    public GlobalSetup()
    {
        var services = new ServiceCollection();

        // Register Medino and scan test assembly
        services.AddMedino(typeof(GlobalSetup).Assembly);

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

[CollectionDefinition("Mediator Tests")]
public class MediatorCollection : ICollectionFixture<GlobalSetup>
{
}