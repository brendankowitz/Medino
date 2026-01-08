# Investigation: NotificationPublishStrategies

**Feature:** enhancements
**Date:** 2026-01-03
**Status:** Complete
**Researcher:** notificationpublishstrategies-researcher

## Summary

This investigation explores implementing configurable notification publish strategies for Medino's `PublishAsync` method, similar to MediatR's `INotificationPublisher` approach introduced in MediatR v12. Currently, Medino uses `Task.WhenAll` to execute all notification handlers in parallel and waits for completion. Configurable strategies would allow developers to choose between sequential execution (ForeachAwait), parallel execution with wait (TaskWhenAll), or custom publishing patterns based on their specific requirements.

## Approach

### Overview

Introduce an `INotificationPublisher` interface and multiple built-in implementations that control how notification handlers are executed. The mediator would use the configured publisher strategy to dispatch notifications to handlers.

### Proposed Architecture

**1. INotificationPublisher Interface**
```csharp
namespace Medino;

/// <summary>
/// Defines how notifications are published to handlers
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes a notification to all registered handlers
    /// </summary>
    /// <typeparam name="TNotification">The notification type</typeparam>
    /// <param name="handlers">The collection of notification handlers</param>
    /// <param name="notification">The notification instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification;
}
```

**2. ForeachAwaitPublisher (Sequential Strategy)**
```csharp
namespace Medino;

/// <summary>
/// Publishes notifications sequentially, stopping on first exception
/// </summary>
public class ForeachAwaitPublisher : INotificationPublisher
{
    public async Task PublishAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification
    {
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}
```

**3. TaskWhenAllPublisher (Parallel Strategy - Current Behavior)**
```csharp
namespace Medino;

/// <summary>
/// Publishes notifications in parallel, waiting for all to complete
/// Aggregates all exceptions if multiple handlers fail
/// </summary>
public class TaskWhenAllPublisher : INotificationPublisher
{
    public async Task PublishAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification
    {
        var tasks = handlers.Select(h => h.HandleAsync(notification, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
```

**4. ForeachAwaitContinueOnExceptionPublisher (Sequential with Exception Handling)**
```csharp
namespace Medino;

/// <summary>
/// Publishes notifications sequentially, continuing on exceptions
/// Aggregates all exceptions at the end
/// </summary>
public class ForeachAwaitContinueOnExceptionPublisher : INotificationPublisher
{
    public async Task PublishAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification
    {
        var exceptions = new List<Exception>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(notification, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}
```

**5. Updated Mediator.PublishAsync**
```csharp
private readonly INotificationPublisher _notificationPublisher;

public Mediator(IServiceProvider serviceProvider, INotificationPublisher? notificationPublisher = null)
{
    _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    _notificationPublisher = notificationPublisher ?? new TaskWhenAllPublisher(); // Default to current behavior
}

public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    where TNotification : INotification
{
    if (notification == null) throw new ArgumentNullException(nameof(notification));

    var handlers = GetServices<INotificationHandler<TNotification>>();
    return _notificationPublisher.PublishAsync(handlers, notification, cancellationToken);
}
```

**6. DI Registration**
```csharp
// ServiceCollectionExtensions.cs
public static IServiceCollection AddMedino(
    this IServiceCollection services,
    Action<MedinoConfiguration>? configure = null)
{
    var configuration = new MedinoConfiguration();
    configure?.Invoke(configuration);

    // Register notification publisher
    services.TryAddSingleton<INotificationPublisher>(
        configuration.NotificationPublisher ?? new TaskWhenAllPublisher());

    // ... rest of registration
}

public class MedinoConfiguration
{
    public INotificationPublisher? NotificationPublisher { get; set; }

    /// <summary>
    /// Use sequential execution (foreach await)
    /// </summary>
    public MedinoConfiguration UseSequentialPublishing()
    {
        NotificationPublisher = new ForeachAwaitPublisher();
        return this;
    }

    /// <summary>
    /// Use parallel execution with Task.WhenAll (default)
    /// </summary>
    public MedinoConfiguration UseParallelPublishing()
    {
        NotificationPublisher = new TaskWhenAllPublisher();
        return this;
    }
}
```

## Pros

### Flexibility and Control

1. **Configurable Behavior:** Developers can choose the execution strategy that best fits their use case (sequential for ordered operations, parallel for performance)
2. **Per-Application Configuration:** Different applications using Medino can configure different strategies based on their specific needs
3. **Extensibility:** Custom publishers can be implemented for specialized scenarios (e.g., priority-based execution, throttling)

### Exception Handling Options

4. **Granular Error Handling:** Different strategies can handle exceptions differently (stop immediately vs. continue and aggregate)
5. **Better Diagnostics:** Sequential execution makes debugging easier when needed
6. **Resilient Publishing:** Continue-on-exception strategies ensure all handlers execute even if some fail

### Performance Optimization

7. **Sequential Performance:** For I/O-bound operations with dependencies, sequential execution can avoid contention
8. **Parallel Performance:** For independent handlers, parallel execution maximizes throughput (current behavior preserved)
9. **Resource Management:** Sequential execution prevents thread pool exhaustion when many notifications are published simultaneously

### Compatibility and Migration

10. **MediatR Compatibility:** Aligns with MediatR v12+ architecture, easing migration for teams familiar with MediatR
11. **Backward Compatible:** Default strategy (TaskWhenAll) maintains current behavior
12. **Gradual Adoption:** Can switch strategies without changing handler implementations

### Medical Software Context

13. **Predictable Execution:** Sequential strategies provide deterministic ordering for audit logs and compliance
14. **Transaction Safety:** Sequential execution works better with database transactions and scoped services
15. **Error Isolation:** Continue-on-exception strategies ensure critical notifications aren't lost due to one handler failure

## Cons

### Complexity and Maintenance

1. **Increased API Surface:** Additional interfaces, classes, and configuration options increase library complexity
2. **Configuration Burden:** Developers must understand different strategies and choose appropriately
3. **Documentation Overhead:** Each strategy needs clear documentation about when to use it and its trade-offs
4. **Testing Complexity:** More strategies mean more test scenarios and edge cases

### Implementation Risks

5. **Breaking Changes Risk:** Modifying `Mediator` constructor could break existing code if not done carefully
6. **Service Lifetime Issues:** Publisher singleton needs to work with transient/scoped handlers correctly
7. **Generic Type Constraints:** IEnumerable exposure in INotificationPublisher interface may cause type safety issues
8. **Reflection Performance:** Handler enumeration and invocation via generic interface may have slight overhead

### Developer Confusion

9. **Choice Paralysis:** Multiple strategies may confuse developers about which to use
10. **Misuse Potential:** Using sequential strategy when parallel is better (or vice versa) can harm performance
11. **Unexpected Behavior:** Changing strategy globally can break assumptions in existing handlers
12. **Exception Handling Surprises:** Different strategies throw exceptions differently (immediate vs. AggregateException)

### Medical Software Concerns

13. **Regulatory Complexity:** Different execution strategies may require different validation/testing approaches for FDA compliance
14. **Transaction Boundary Issues:** Parallel execution with database transactions can cause deadlocks or data inconsistencies
15. **Audit Trail Gaps:** Parallel execution makes establishing clear audit trail ordering more difficult
16. **Scoped Service Problems:** DbContext and other scoped services may not work correctly with parallel execution

### Limited Value for Common Cases

17. **Most Handlers Are Independent:** In practice, most notification handlers are independent and benefit from parallel execution
18. **Premature Optimization:** Sequential strategy optimization may be solving a problem that doesn't exist
19. **External Solutions Better:** For complex orchestration, message queues (RabbitMQ, Azure Service Bus) are more appropriate
20. **Simple Alternative:** Handlers can coordinate internally if needed (e.g., using distributed locks)

## Technical Details

### Current Implementation Analysis

The current `PublishAsync` in Medino (lines 254-269 of Mediator.cs):
- Uses `Task.WhenAll` for parallel execution
- Waits for all handlers to complete
- No explicit exception handling (exceptions bubble up)
- No handler ordering guarantees
- Works well for independent handlers
- Comment says "fire and forget" but actually waits

### Implementation Requirements

**1. Interface Design**
- `INotificationPublisher` must be generic to support any `INotification` type
- Handlers passed as `IEnumerable<INotificationHandler<TNotification>>`
- Must support `CancellationToken` propagation
- Should not expose handler resolution logic (mediator responsibility)

**2. Exception Handling Patterns**

```csharp
// Stop on first exception (ForeachAwaitPublisher)
foreach (var handler in handlers)
{
    await handler.HandleAsync(notification, ct); // Throws immediately
}

// Continue on exception (ForeachAwaitContinueOnExceptionPublisher)
var exceptions = new List<Exception>();
foreach (var handler in handlers)
{
    try { await handler.HandleAsync(notification, ct); }
    catch (Exception ex) { exceptions.Add(ex); }
}
if (exceptions.Any()) throw new AggregateException(exceptions);

// Parallel with aggregated exceptions (TaskWhenAllPublisher)
await Task.WhenAll(tasks); // Throws AggregateException if any fail
```

**3. Service Registration**

Publishers should be registered as **singletons** because:
- They have no state
- They're reused across all mediator invocations
- Handler instances are resolved per call (transient/scoped)

**4. Backward Compatibility**

To avoid breaking changes:
```csharp
// Old constructor still works
public Mediator(IServiceProvider serviceProvider)
    : this(serviceProvider, null) { }

// New constructor with optional publisher
public Mediator(IServiceProvider serviceProvider, INotificationPublisher? notificationPublisher)
{
    _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    _notificationPublisher = notificationPublisher ?? new TaskWhenAllPublisher();
}
```

### Performance Considerations

**Sequential (ForeachAwait):**
- Memory: Minimal (no task array allocation)
- Latency: Sum of all handler durations
- Best for: Ordered operations, resource-constrained environments

**Parallel (TaskWhenAll):**
- Memory: Task array allocation (~32 bytes per task)
- Latency: Duration of slowest handler
- Best for: Independent handlers, I/O-bound operations

**Benchmark Example:**
- 5 handlers, each takes 100ms
- Sequential: ~500ms total
- Parallel: ~100ms total (5x faster)

### Thread Safety and Concurrency

- Publishers themselves are stateless and thread-safe
- Handler implementations must be thread-safe if using parallel strategies
- DbContext issues: EF Core DbContext is NOT thread-safe
  - Parallel handlers sharing same DbContext scope will fail
  - Mitigation: Each handler should inject its own scoped DbContext or use sequential publishing

### Integration with Existing Features

**Pipeline Behaviors:**
- Not applicable (behaviors only work with `IRequest<TResponse>`, not `INotification`)

**Exception Handlers:**
- Not applicable (exception handlers only work with requests)
- Notification exceptions must be handled by the publishing strategy or in handlers themselves

## Dependencies

### New Dependencies

1. **None** - All required types are in .NET BCL (Task, IEnumerable, AggregateException)

### Internal Dependencies

2. **New Files:**
   - `src/Medino/INotificationPublisher.cs` (interface)
   - `src/Medino/NotificationPublishers/ForeachAwaitPublisher.cs`
   - `src/Medino/NotificationPublishers/TaskWhenAllPublisher.cs`
   - `src/Medino/NotificationPublishers/ForeachAwaitContinueOnExceptionPublisher.cs`

3. **Modified Files:**
   - `src/Medino/Mediator.cs` (add INotificationPublisher field and use it)
   - `src/Medino.Extensions.DependencyInjection/ServiceCollectionExtensions.cs` (add configuration)
   - `src/Medino.Extensions.DependencyInjection/MedinoConfiguration.cs` (new configuration class)

4. **Test Files:**
   - `src/Medino.Tests/NotificationPublishers/ForeachAwaitPublisherTests.cs`
   - `src/Medino.Tests/NotificationPublishers/TaskWhenAllPublisherTests.cs`
   - `src/Medino.Tests/NotificationPublishers/ContinueOnExceptionPublisherTests.cs`
   - `src/Medino.Tests/NotificationPublishers/ConfigurationTests.cs`

### Documentation Updates

5. **README.md** - Add section on notification publishing strategies
6. **MIGRATION.md** - Document MediatR INotificationPublisher migration
7. **XML Documentation** - Add comprehensive XML docs for all new types

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| **Breaking Changes** - Modifying Mediator constructor breaks existing code | Low | High | Provide parameterless constructor overload; ensure backward compatibility through optional parameters and default values |
| **DbContext Concurrency Issues** - Parallel handlers fail with shared DbContext | High | High | Document clearly in README; provide warning in XML docs; recommend sequential strategy or separate scopes for database operations |
| **Developer Confusion** - Wrong strategy chosen for use case | Medium | Medium | Provide decision tree in documentation; sensible default (TaskWhenAll preserves current behavior); clear examples for each strategy |
| **AggregateException Handling** - Developers unfamiliar with exception aggregation | Medium | Low | Document exception handling patterns; provide examples in tests; consider logging recommendations |
| **Performance Regression** - Sequential strategy used where parallel is better | Low | Medium | Default to current parallel behavior; document performance characteristics; provide benchmarks |
| **Testing Complexity** - More strategies mean more test scenarios | Medium | Low | Comprehensive test suite covering all strategies; shared test fixtures to reduce duplication |
| **Transaction Boundary Issues** - Parallel handlers cause database deadlocks | High | High | Document transaction limitations prominently; recommend sequential for transactional scenarios; provide examples |
| **Medical Compliance** - Different strategies require different validation | Medium | High | Document compliance considerations; recommend conservative defaults; coordinate with QA/regulatory teams |
| **Ordering Assumptions** - Code assumes sequential execution breaks with parallel | Low | Medium | Document non-deterministic ordering of parallel strategy; recommend sequential if order matters |
| **Silent Adoption** - Developers don't realize behavior changed | Low | Medium | Make strategy explicit in configuration; add logging of chosen strategy at startup |

## Effort Estimate

### Development: 3-4 days

- **Day 1:** Core Implementation
  - Design and implement `INotificationPublisher` interface
  - Implement `ForeachAwaitPublisher` (sequential)
  - Implement `TaskWhenAllPublisher` (parallel - extract from existing)
  - Implement `ForeachAwaitContinueOnExceptionPublisher` (sequential with exception handling)
  - Add XML documentation to all new types

- **Day 2:** Integration
  - Modify `Mediator.cs` to use `INotificationPublisher`
  - Ensure backward compatibility with existing constructors
  - Create `MedinoConfiguration` class with fluent API
  - Update `ServiceCollectionExtensions.AddMedino()` for configuration
  - Verify existing tests still pass

- **Day 3:** Testing
  - Unit tests for each publisher implementation
  - Exception handling tests (immediate throw vs. aggregate)
  - Configuration tests (DI registration, default behavior)
  - Integration tests with multiple handlers
  - Performance benchmarks (sequential vs. parallel)

- **Day 4:** Refinement and Code Review
  - Address code review feedback
  - Refine XML documentation
  - Add edge case tests
  - Performance optimization if needed

### Testing: 2-3 days

- **Day 1:** Functional Testing
  - Sequential execution verification (order preserved)
  - Parallel execution verification (all handlers execute)
  - Exception handling for each strategy
  - Empty handler list edge case
  - Null notification validation
  - Cancellation token propagation

- **Day 2:** Integration Testing
  - DI container registration and resolution
  - Configuration API (fluent methods)
  - Backward compatibility (existing code works)
  - Multiple notification types
  - Scoped service integration (DbContext scenarios)

- **Day 3:** Performance and Stress Testing
  - Benchmark sequential vs. parallel strategies
  - High volume notification publishing
  - Memory allocation profiling
  - Thread pool utilization
  - Concurrent mediator usage

### Documentation: 2 days

- **Day 1:** API and Usage Documentation
  - README.md section on notification publishing strategies
  - When to use each strategy (decision tree)
  - Code examples for each publisher
  - Configuration examples
  - Best practices for handler implementation

- **Day 2:** Migration and Advanced Topics
  - MIGRATION.md update for MediatR users
  - Transaction boundary considerations
  - DbContext concurrency guidance
  - Exception handling patterns
  - Performance tuning recommendations
  - Medical software compliance notes

**Total Estimated Effort: 7-9 days**

## Recommendation

### ✅ RECOMMENDED as Optional Enhancement with Caveats

NotificationPublishStrategies is **recommended** as an optional enhancement to Medino, but with important implementation guidelines and caveats for medical software use.

### Why Recommended

1. **Aligns with Industry Standards:** MediatR v12+ uses this exact pattern, making Medino familiar to developers migrating from MediatR

2. **Preserves Current Behavior:** Default to `TaskWhenAllPublisher` maintains existing parallel execution, ensuring zero breaking changes

3. **Solves Real Problems:**
   - Sequential execution needed for ordered operations (audit logs, event sourcing)
   - Exception isolation with continue-on-exception strategies
   - Resource management in high-load scenarios

4. **Low Implementation Risk:**
   - Simple abstraction with clear separation of concerns
   - No external dependencies required
   - Backward compatible through constructor overloading

5. **Extensibility:** Opens door for custom publishers (throttling, priority-based, retry logic) without changing core library

### Implementation Guidelines

**1. Conservative Defaults**
```csharp
// Default to current behavior
services.AddMedino(); // Uses TaskWhenAllPublisher (parallel)
```

**2. Explicit Configuration**
```csharp
// Make strategy choice explicit and visible
services.AddMedino(cfg => cfg.UseSequentialPublishing());
```

**3. Comprehensive Documentation**
- Decision tree for choosing strategy
- Clear warnings about DbContext concurrency
- Transaction boundary guidance
- Medical compliance considerations

**4. Strategy Recommendations by Scenario**

| Scenario | Recommended Strategy | Reasoning |
|----------|---------------------|-----------|
| **Independent handlers** (logging, email, SMS) | TaskWhenAll (parallel) | Maximum performance, no coordination needed |
| **Ordered operations** (audit logs, event sourcing) | ForeachAwait (sequential) | Guaranteed ordering, predictable |
| **Database operations** (DbContext updates) | ForeachAwait (sequential) | Avoid DbContext concurrency issues |
| **Critical notifications** (must all succeed) | ForeachAwait (sequential, stop on first) | Early failure detection |
| **Resilient notifications** (best-effort delivery) | ForeachAwaitContinueOnException | All handlers execute even if some fail |
| **Custom orchestration** | Custom INotificationPublisher | Specialized requirements |

### Medical Software Considerations

**For Medical/Healthcare Applications:**

1. **Regulatory Compliance:**
   - Sequential execution provides deterministic audit trails (required for FDA 21 CFR Part 11)
   - Easier to validate and test than parallel execution
   - Clear exception handling meets medical device software requirements (IEC 62304)

2. **Data Integrity:**
   - Sequential strategies prevent DbContext concurrency issues
   - Transaction boundaries are clear and predictable
   - Rollback semantics are well-defined

3. **Recommended Default for Medical Software:**
```csharp
// Conservative default for medical applications
services.AddMedino(cfg => cfg.UseSequentialPublishing());
```

4. **Critical Notifications:**
   - Use `ForeachAwaitPublisher` (stop on first exception) for critical medical events
   - Ensures immediate failure detection
   - Prevents partial state updates

### What NOT to Implement

**Do NOT implement fire-and-forget strategies** as part of this enhancement:
- Fire-and-forget belongs in external message queues (see fire-and-forget investigation)
- Silent failures are unacceptable for medical software
- All strategies in this investigation wait for handler completion

### Success Criteria

1. ✅ Zero breaking changes to existing API
2. ✅ Default behavior identical to current implementation
3. ✅ All existing tests pass without modification
4. ✅ Comprehensive documentation with decision tree
5. ✅ Medical software guidance prominent in README
6. ✅ MediatR migration path documented
7. ✅ Performance benchmarks show expected behavior
8. ✅ DbContext concurrency warnings in XML docs

### Enhancement: Per-Call Strategy Override

**User Feedback:** The investigation should also support per-call strategy override in addition to configured defaults, as it makes sense to allow override per call.

**Proposed Fluent API Extension:**

```csharp
// Extension methods for per-call strategy override
public static class MediatorPublishExtensions
{
    public static Task PublishSequentialAsync<TNotification>(
        this IMediator mediator,
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        var publisher = new ForeachAwaitPublisher();
        return PublishWithStrategyAsync(mediator, notification, publisher, cancellationToken);
    }

    public static Task PublishParallelAsync<TNotification>(
        this IMediator mediator,
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        var publisher = new TaskWhenAllPublisher();
        return PublishWithStrategyAsync(mediator, notification, publisher, cancellationToken);
    }

    public static Task PublishWithContinueOnExceptionAsync<TNotification>(
        this IMediator mediator,
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        var publisher = new ForeachAwaitContinueOnExceptionPublisher();
        return PublishWithStrategyAsync(mediator, notification, publisher, cancellationToken);
    }

    public static Task PublishWithStrategyAsync<TNotification>(
        this IMediator mediator,
        TNotification notification,
        INotificationPublisher strategy,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification == null) throw new ArgumentNullException(nameof(notification));
        if (strategy == null) throw new ArgumentNullException(nameof(strategy));

        // Access service provider through mediator to get handlers
        var serviceProvider = ((Mediator)mediator).ServiceProvider;
        var handlers = serviceProvider.GetServices<INotificationHandler<TNotification>>();
        return strategy.PublishAsync(handlers, notification, cancellationToken);
    }
}
```

**Alternative Fluent Chain API:**

```csharp
// Fluent builder pattern for more readable syntax
public static class MediatorFluentExtensions
{
    public static IPublishBuilder<TNotification> Publish<TNotification>(
        this IMediator mediator,
        TNotification notification)
        where TNotification : INotification
    {
        return new PublishBuilder<TNotification>(mediator, notification);
    }
}

public interface IPublishBuilder<TNotification> where TNotification : INotification
{
    Task SequentialAsync(CancellationToken cancellationToken = default);
    Task ParallelAsync(CancellationToken cancellationToken = default);
    Task ContinueOnExceptionAsync(CancellationToken cancellationToken = default);
    Task WithStrategyAsync(INotificationPublisher strategy, CancellationToken cancellationToken = default);
}

// Usage:
await mediator.Publish(notification).ParallelAsync();        // Same as WhenAll
await mediator.Publish(notification).SequentialAsync();
await mediator.Publish(notification).ContinueOnExceptionAsync();
await mediator.Publish(notification).WithStrategyAsync(customPublisher);
```

**Benefits of Per-Call Override:**

1. **Contextual Control:** Different notifications may need different strategies even within the same application
2. **Testing Flexibility:** Easy to test specific scenarios with different strategies
3. **Migration Aid:** Gradual migration from default to specific strategies per notification type
4. **Performance Tuning:** Optimize specific hot paths without affecting entire application

**Implementation Considerations:**

- **ServiceProvider Access:** Extension methods need access to service provider to resolve handlers
- **Strategy Instance Management:** Per-call creates new strategy instances (acceptable since they're stateless)
- **Type Safety:** Generic constraints ensure compile-time type safety
- **Backward Compatibility:** Regular `PublishAsync` still uses configured default strategy

**Updated Architecture:**

```csharp
// Core mediator still uses configured default
public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    where TNotification : INotification
{
    var handlers = GetServices<INotificationHandler<TNotification>>();
    return _notificationPublisher.PublishAsync(handlers, notification, cancellationToken);
}

// Extensions provide per-call overrides
// await mediator.PublishAsync(notification);              // Uses configured default
// await mediator.PublishParallelAsync(notification);      // Per-call override
// await mediator.Publish(notification).SequentialAsync(); // Fluent override
```

**Effort Impact:**
- **Additional Development:** +1 day for extension methods and builder pattern
- **Additional Testing:** +0.5 day for extension method tests
- **Documentation:** +0.5 day for fluent API examples

**Recommendation Enhancement:**
Keep the configured default approach as primary, but add per-call override extensions for flexibility. This provides both the predictable configuration-driven behavior needed for medical software and the contextual control that developers may need for specific scenarios.

### Next Steps

1. **Review with stakeholders** to confirm strategy set meets requirements
2. **Decide on fluent API style** (extension methods vs. builder pattern vs. both)
3. **Create prototype** of INotificationPublisher interface + fluent extensions
4. **Write tests first** (TDD approach) for each strategy and extension methods
5. **Implement incrementally** starting with interface and TaskWhenAllPublisher
6. **Update documentation** alongside implementation including fluent API examples
7. **Code review** with focus on medical software implications and API usability
8. **Performance testing** to validate no regressions with extension overhead

## References

### MediatR Publishing Strategies

- [How To Publish MediatR Notifications In Parallel](https://www.milanjovanovic.tech/blog/how-to-publish-mediatr-notifications-in-parallel)
- [Publish MediatR Notifications in Parallel - Code Maze](https://code-maze.com/mediatr-parallel-publishing-notifications/)
- [Publishing strategies in MediatR | Fati Iseni](https://fiseni.com/posts/publishing-strategies-in-MediatR/)
- [How to change MediatR publish strategy · Discussion #736](https://github.com/jbogard/MediatR/discussions/736)
- [MediatR ForeachAwaitPublisher.cs - GitHub](https://github.com/jbogard/MediatR/blob/master/src/MediatR/NotificationPublishers/ForeachAwaitPublisher.cs)

### Parallel vs Sequential Execution

- [How in MediatR we can have events (Notifications) async and completely real Parallel | Medium](https://medium.com/@mohsen_rajabi/how-in-mediatr-we-can-have-events-notifications-async-and-completely-real-parallel-2068f24912e6)
- [Synchronous vs asynchronous communications | TechTarget](https://www.techtarget.com/searchapparchitecture/tip/Synchronous-vs-asynchronous-communication-The-differences)
- [Asynchronous vs. Synchronous Processing in System Design | Sai's Notebook](https://sai-tai.com/software-development/system-design/sync-async/)

### Exception Handling and Best Practices

- [Building a Better MediatR Publisher With Channels (and why you shouldn't)](https://www.milanjovanovic.tech/blog/building-a-better-mediatr-publisher-with-channels-and-why-you-shouldnt)
- [MediatR — Beyond the basics | Medium](https://medium.com/@cristian_lopes/mediatr-beyond-the-basics-8ab90841a732)

### Medical Software Context

- [Asynchronous vs. Synchronous - AAHA](https://www.aaha.org/aaha-guidelines/telehealth-guidelines/considerations-for-choosing-technology/asynchronous-vs.-synchronous/)
- [Synchronous vs. Asynchronous Healthcare - HealthSnap](https://healthsnap.io/synchronous-vs-asynchronous-healthcare-whats-the-difference/)
