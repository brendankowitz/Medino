# Investigation: parallel-sequential-publishing-patterns

**Feature:** mediator
**Date:** 2026-01-05
**Status:** Explorations

## Summary

Investigation into whether notification publishing should execute handlers in parallel (current implementation using `Task.WhenAll()`) or sequentially, and whether to provide configuration options for different publishing strategies based on use case requirements.

## Approach

**Current Implementation Analysis:**
The current Medino implementation uses parallel execution via `Task.WhenAll()` in `PublishAsync()`:

```csharp
var tasks = handlers.Select(handler => handler.HandleAsync(notification, cancellationToken));
await Task.WhenAll(tasks).ConfigureAwait(false);
```

**Alternative Strategies Considered:**

1. **Parallel Execution (Current)** - All handlers execute concurrently
2. **Sequential Execution** - Handlers execute one after another using `foreach` + `await`
3. **Configurable Strategy** - Allow per-notification or global configuration of execution strategy
4. **Fail-Fast vs Continue-on-Error** - How to handle exceptions in multi-handler scenarios

## Pros

**Parallel Execution (Current Approach):**
- **Performance**: Maximum throughput when handlers are I/O bound or independent
- **Scalability**: Better resource utilization in high-throughput scenarios
- **Simplicity**: Single implementation, consistent behavior
- **Industry Standard**: Matches behavior of most modern mediator libraries (MediatR, etc.)

**Sequential Execution:**
- **Predictable Ordering**: Handlers execute in registration/discovery order
- **Resource Control**: Limited concurrent resource usage (databases, external APIs)
- **Error Isolation**: Single handler failure doesn't affect others in flight
- **Debugging**: Easier to trace execution flow and diagnose issues

## Cons

**Parallel Execution (Current Approach):**
- **Resource Contention**: Multiple handlers may overwhelm shared resources (database connections)
- **Error Complexity**: If one handler fails, others continue executing (partial completion)
- **Ordering Uncertainty**: No guaranteed execution order between handlers
- **Debugging Complexity**: Concurrent execution makes tracing more difficult

**Sequential Execution:**
- **Performance Penalty**: Handlers block each other, reducing overall throughput
- **Scalability Limitations**: Does not leverage available concurrency
- **Single Point of Failure**: Early handler failure can block later handlers
- **Latency Impact**: Total execution time is sum of all handler times

## Technical Details

**Current Parallel Implementation:**
```csharp
public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    where TNotification : INotification
{
    if (notification == null) throw new ArgumentNullException(nameof(notification));

    var handlers = GetServices<INotificationHandler<TNotification>>().ToList();

    if (handlers.Count == 0)
    {
        return; // No handlers is OK for notifications
    }

    var tasks = handlers.Select(handler => handler.HandleAsync(notification, cancellationToken));
    await Task.WhenAll(tasks).ConfigureAwait(false);
}
```

**Alternative Sequential Implementation:**
```csharp
public async Task PublishSequentialAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
{
    var handlers = GetServices<INotificationHandler<TNotification>>();

    foreach (var handler in handlers)
    {
        await handler.HandleAsync(notification, cancellationToken).ConfigureAwait(false);
    }
}
```

**Exception Handling Considerations:**
- **Parallel**: `Task.WhenAll()` throws aggregate exception with all handler failures
- **Sequential**: First handler exception stops execution, subsequent handlers don't run
- **Continue-on-Error**: Would require custom exception handling wrapper

## Dependencies

- **No new dependencies** for keeping current parallel approach
- **Configuration system** if implementing configurable strategies (Microsoft.Extensions.Options)
- **Breaking change** if changing default behavior (requires major version bump)
- **Additional testing** for new execution patterns and exception scenarios

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Breaking API changes if switching default behavior | High | High | Keep parallel as default, add optional sequential methods |
| Resource exhaustion with parallel handlers | Medium | Medium | Document best practices, provide guidance on handler design |
| Complex exception handling with parallel execution | Medium | High | Use AggregateException properly, provide clear error messages |
| Performance regression if switching to sequential | High | Medium | Benchmark both approaches, provide configuration options |
| Inconsistent behavior between notification types | Low | High | Maintain consistent defaults, clear documentation |

## Effort Estimate

**Option 1: Keep Current Parallel Implementation**
- **Development:** 0 days (no changes)
- **Testing:** 1 day (verify current behavior, add edge case tests)
- **Documentation:** 0.5 days (document current behavior and trade-offs)

**Option 2: Add Optional Sequential Publishing Methods**
- **Development:** 2 days (implement sequential methods, configuration)
- **Testing:** 3 days (test both patterns, exception scenarios, performance)
- **Documentation:** 1 day (document both strategies, usage guidance)

**Option 3: Full Configurable Strategy System**
- **Development:** 5 days (design configuration, implement strategies, DI integration)
- **Testing:** 5 days (comprehensive testing, performance benchmarks)
- **Documentation:** 2 days (architecture documentation, migration guide)

## Recommendation

**NEW RECOMMENDED APPROACH: Fluent API Extensions (Option 4)**

**Fluent API Design:**
Implement fluent API extensions that provide explicit control over execution behavior while maintaining full backward compatibility:

```csharp
// Backward compatible - existing calls continue to work unchanged
await mediator.PublishAsync(notification);

// New fluent API options
await mediator.PublishAsync(notification).WithParallelExecution();
await mediator.PublishAsync(notification).WithSequentialExecution();
await mediator.PublishAsync(notification).WithContinueOnError();

// Chaining multiple behaviors
await mediator.PublishAsync(notification)
    .WithSequentialExecution()
    .WithContinueOnError();
```

**Implementation Components:**
1. **INotificationPublisher Interface** - Fluent object that's directly awaitable
2. **Extension Methods** - Add fluent methods to IMediator without breaking existing API
3. **Strategy Pattern** - Internal publisher implementation with pluggable execution strategies
4. **Comprehensive Testing** - All execution modes and error scenarios
5. **Documentation Updates** - Usage examples and best practices

**Key Benefits:**
- **Zero Breaking Changes**: Existing `PublishAsync()` calls work unchanged
- **Explicit Control**: Developers can choose execution strategy per call
- **Discoverability**: IntelliSense reveals fluent options after `PublishAsync()`
- **Extensibility**: Easy to add new execution strategies in the future
- **Performance**: Minimal overhead for fluent chain
- **Industry Pattern**: Follows established fluent API patterns in .NET

**Effort Estimate for Fluent API:**
- **Development:** 3 days (interfaces, implementation, extensions)
- **Testing:** 4 days (all strategies, error handling, performance validation)
- **Documentation:** 1.5 days (API docs, examples, migration guide)

This approach supersedes the previous recommendation as it provides the best of all worlds: backward compatibility, explicit control, and future extensibility.

## References

- [Medino Mediator.cs Implementation](../../../src/Medino/Mediator.cs) - Lines 254-268
- [MediatR Publisher Strategies](https://github.com/jbogard/MediatR/wiki/Behaviors#publisher-strategies) - Reference implementation
- [.NET Task.WhenAll Documentation](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall) - Parallel execution semantics
- [Exception Handling in Parallel Tasks](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/exception-handling-task-parallel-library) - AggregateException behavior
- [Medino Test Coverage](../../../TEST_COVERAGE.md) - Current notification testing scenarios
