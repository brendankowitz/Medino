using Microsoft.Extensions.DependencyInjection;

namespace Medino.Extensions.DependencyInjection;

/// <summary>
/// Adapter that wraps IServiceProvider for use with Mediator
/// </summary>
internal class MediatorServiceProviderAdapter : IMediatorServiceProvider
{
    private readonly IServiceProvider _serviceProvider;

    public MediatorServiceProviderAdapter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public T? GetService<T>() where T : class
    {
        return _serviceProvider.GetService(typeof(T)) as T;
    }

    public object? GetService(Type serviceType)
    {
        return _serviceProvider.GetService(serviceType);
    }

    public IEnumerable<T> GetServices<T>() where T : class
    {
        return _serviceProvider.GetServices(typeof(T)).OfType<T>();
    }

    public IEnumerable<object> GetServices(Type serviceType)
    {
        return _serviceProvider.GetServices(serviceType).Where(s => s != null)!;
    }
}