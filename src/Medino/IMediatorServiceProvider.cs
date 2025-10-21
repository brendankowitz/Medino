namespace Medino;

/// <summary>
/// Abstraction for service resolution used by the mediator
/// </summary>
public interface IMediatorServiceProvider
{
    /// <summary>
    /// Gets a service of the specified type
    /// </summary>
    /// <typeparam name="T">The type of service to resolve</typeparam>
    /// <returns>The service instance, or null if not found</returns>
    T? GetService<T>() where T : class;
    
    /// <summary>
    /// Gets a service of the specified type
    /// </summary>
    /// <returns>The service instance, or null if not found</returns>
    object? GetService(Type serviceType);

    /// <summary>
    /// Gets all services of the specified type
    /// </summary>
    /// <typeparam name="T">The type of service to resolve</typeparam>
    /// <returns>An enumerable of all matching service instances</returns>
    IEnumerable<T> GetServices<T>() where T : class;
    
    /// <summary>
    /// Gets all services of the specified type
    /// </summary>
    /// <returns>An enumerable of all matching service instances</returns>
    IEnumerable<object> GetServices(Type serviceType);
}