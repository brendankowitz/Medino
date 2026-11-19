using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Medino.Extensions.DependencyInjection;

/// <summary>
/// Extensions for configuring Medino mediator in Microsoft.Extensions.DependencyInjection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Medino services to the specified <see cref="IServiceCollection"/>.
    /// Scans the calling assembly for handlers.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddMedino(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddMedino(Assembly.GetCallingAssembly());
    }

    /// <summary>
    /// Adds Medino services to the specified <see cref="IServiceCollection"/>.
    /// Command and request handlers found by scanning must be unique per closed handler interface;
    /// open generic types are skipped.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="assemblies">Assemblies to scan for handlers</param>
    /// <returns>The service collection</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="assemblies"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="assemblies"/> is empty or contains a null element.</exception>
    /// <exception cref="InvalidOperationException">Two different classes implement the same command or request handler interface.</exception>
    public static IServiceCollection AddMedino(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        if (Array.IndexOf(assemblies, null) >= 0)
        {
            throw new ArgumentException("Assemblies must not contain null elements.", nameof(assemblies));
        }

        if (assemblies.Length == 0)
        {
            throw new ArgumentException("No assemblies found to scan. Supply at least one assembly.", nameof(assemblies));
        }

        // Register mediator
        services.TryAddTransient<IMediator, Mediator>();

        // Scan and register handlers
        RegisterHandlers(services, assemblies);
        RegisterNotificationHandlers(services, assemblies);
        RegisterPipelineBehaviors(services, assemblies);
        RegisterContextPipelineBehaviors(services, assemblies);
        RegisterExceptionHandlers(services, assemblies);
        RegisterExceptionActions(services, assemblies);

        return services;
    }

    /// <summary>
    /// Adds Medino services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration action</param>
    /// <returns>The service collection</returns>
    public static IServiceCollection AddMedino(this IServiceCollection services, Action<MediatorConfiguration> configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var config = new MediatorConfiguration();
        configuration(config);

        if (!config.Assemblies.Any())
        {
            throw new ArgumentException("No assemblies found to scan. Supply at least one assembly.");
        }

        return services.AddMedino(config.Assemblies.ToArray());
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        // Register command handlers
        var commandHandlerType = typeof(ICommandHandler<>);
        var requestHandlerType = typeof(IRequestHandler<,>);
        var registeredHandlers = new Dictionary<Type, Type>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .ToList();

            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces();

                // Register command handlers
                foreach (var @interface in interfaces)
                {
                    if (@interface.IsGenericType)
                    {
                        var genericTypeDef = @interface.GetGenericTypeDefinition();

                        if (genericTypeDef == commandHandlerType || genericTypeDef == requestHandlerType)
                        {
                            // Same implementation seen again (e.g. assembly passed twice) is a no-op;
                            // a different one would silently shadow the first in the container.
                            if (registeredHandlers.TryGetValue(@interface, out var existing))
                            {
                                if (existing != type)
                                {
                                    throw new InvalidOperationException(
                                        $"Multiple handlers found for {@interface}: {existing} and {type}. " +
                                        "Command and request handlers must have exactly one implementation.");
                                }
                            }
                            else
                            {
                                registeredHandlers.Add(@interface, type);
                                services.AddTransient(@interface, type);
                            }
                        }
                    }
                }
            }
        }
    }

    private static void RegisterNotificationHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var notificationHandlerType = typeof(INotificationHandler<>);
        var registeredHandlers = new HashSet<(Type interfaceType, Type implementationType)>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .ToList();

            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == notificationHandlerType)
                    .ToList();

                foreach (var @interface in interfaces)
                {
                    // Use AddTransient (not TryAddTransient) to allow multiple handlers per notification
                    // Track registrations to avoid duplicates
                    if (registeredHandlers.Add((@interface, type)))
                    {
                        services.AddTransient(@interface, type);
                    }
                }
            }
        }
    }

    private static void RegisterPipelineBehaviors(IServiceCollection services, Assembly[] assemblies)
    {
        var pipelineBehaviorType = typeof(IPipelineBehavior<,>);
        var registeredBehaviors = new HashSet<(Type interfaceType, Type implementationType)>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .ToList();

            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == pipelineBehaviorType)
                    .ToList();

                foreach (var @interface in interfaces)
                {
                    // Track registrations to avoid duplicates
                    if (registeredBehaviors.Add((@interface, type)))
                    {
                        services.AddTransient(@interface, type);
                    }
                }
            }
        }
    }

    private static void RegisterContextPipelineBehaviors(IServiceCollection services, Assembly[] assemblies)
    {
        var contextPipelineBehaviorType = typeof(IContextPipelineBehavior<,>);
        var registeredBehaviors = new HashSet<(Type interfaceType, Type implementationType)>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .ToList();

            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == contextPipelineBehaviorType)
                    .ToList();

                foreach (var @interface in interfaces)
                {
                    // IContextPipelineBehavior<object, TResponse> can never be invoked: PipelineContext<T>
                    // is invariant, so the PipelineContext<TRequest> created at send time is not assignable
                    // to the PipelineContext<object> parameter its HandleAsync expects. Silently registering
                    // one would leave it wired up but never executed, so fail fast here instead.
                    if (@interface.GetGenericArguments()[0] == typeof(object))
                    {
                        throw new InvalidOperationException(
                            $"{type.Name} implements IContextPipelineBehavior<object, ...>, which can never be invoked " +
                            "because PipelineContext<T> is invariant. Strongly-type the context behavior to a concrete " +
                            "request type, or use IPipelineBehavior<object, TResponse> for request-type-agnostic behaviors.");
                    }

                    // Track registrations to avoid duplicates
                    if (registeredBehaviors.Add((@interface, type)))
                    {
                        services.AddTransient(@interface, type);
                    }
                }
            }
        }
    }

    private static void RegisterExceptionHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var exceptionHandlerType = typeof(IRequestExceptionHandler<,,>);
        var registeredHandlers = new HashSet<(Type interfaceType, Type implementationType)>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .ToList();

            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == exceptionHandlerType)
                    .ToList();

                foreach (var @interface in interfaces)
                {
                    // Track registrations to avoid duplicates
                    if (registeredHandlers.Add((@interface, type)))
                    {
                        services.AddTransient(@interface, type);
                    }
                }
            }
        }
    }

    private static void RegisterExceptionActions(IServiceCollection services, Assembly[] assemblies)
    {
        var exceptionActionType = typeof(IRequestExceptionAction<,>);
        var registeredActions = new HashSet<(Type interfaceType, Type implementationType)>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .ToList();

            foreach (var type in types)
            {
                var interfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == exceptionActionType)
                    .ToList();

                foreach (var @interface in interfaces)
                {
                    // Track registrations to avoid duplicates
                    if (registeredActions.Add((@interface, type)))
                    {
                        services.AddTransient(@interface, type);
                    }
                }
            }
        }
    }
}

/// <summary>
/// Configuration for the mediator
/// </summary>
public class MediatorConfiguration
{
    internal List<Assembly> Assemblies { get; } = new();

    /// <summary>
    /// Register services from the specified assemblies
    /// </summary>
    /// <param name="assemblies">Assemblies to scan</param>
    /// <returns>This configuration</returns>
    public MediatorConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        if (Array.IndexOf(assemblies, null) >= 0)
        {
            throw new ArgumentException("Assemblies must not contain null elements.", nameof(assemblies));
        }

        Assemblies.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Register services from assemblies containing the specified types
    /// </summary>
    /// <param name="types">Types whose assemblies should be scanned</param>
    /// <returns>This configuration</returns>
    public MediatorConfiguration RegisterServicesFromAssemblyContaining(params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(types);

        if (Array.IndexOf(types, null) >= 0)
        {
            throw new ArgumentException("Types must not contain null elements.", nameof(types));
        }

        Assemblies.AddRange(types.Select(t => t.Assembly).Distinct());
        return this;
    }

    /// <summary>
    /// Register services from assembly containing the specified type
    /// </summary>
    /// <typeparam name="T">Type whose assembly should be scanned</typeparam>
    /// <returns>This configuration</returns>
    public MediatorConfiguration RegisterServicesFromAssemblyContaining<T>()
    {
        Assemblies.Add(typeof(T).Assembly);
        return this;
    }
}