# Investigation: Fire-and-Forget Publishing

**Feature:** enhancements
**Date:** 2026-01-03
**Status:** Complete
**Researcher:** research-fire-and-forget-publishing-researcher

## Summary

This investigation explores implementing fire-and-forget publishing for `PublishAsync` in Medino, where notification handlers execute asynchronously without blocking the caller. Currently, `PublishAsync` waits for all handlers to complete using `Task.WhenAll`, which can create performance bottlenecks when handlers perform slow operations (emails, SMS, logging, external API calls). Fire-and-forget publishing would allow the calling code to continue immediately while handlers execute in the background.

## Approach

### Overview

Modify the `PublishAsync` method in `Mediator.cs` to support optional fire-and-forget execution through one of several implementation strategies:

### Strategy 1: Simple Task.Run (Naive Approach)
```csharp
public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    where TNotification : INotification
{
    if (notification == null) throw new ArgumentNullException(nameof(notification));

    var handlers = GetServices<INotificationHandler<TNotification>>().ToList();

    if (handlers.Count == 0)
    {
        return Task.CompletedTask;
    }

    // Fire and forget - don't await
    _ = Task.Run(async () =>
    {
        var tasks = handlers.Select(handler => handler.HandleAsync(notification, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }, cancellationToken);

    return Task.CompletedTask;
}
```

### Strategy 2: Channel-Based Background Queue (Recommended)
```csharp
// New interface
public interface INotificationQueue
{
    ValueTask QueueNotificationAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}

// Background queue implementation
public class NotificationBackgroundQueue : INotificationQueue
{
    private readonly Channel<NotificationWorkItem> _queue;

    public NotificationBackgroundQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<NotificationWorkItem>(options);
    }

    public async ValueTask QueueNotificationAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        var workItem = new NotificationWorkItem(notification, typeof(TNotification), cancellationToken);
        await _queue.Writer.WriteAsync(workItem, cancellationToken);
    }

    public async ValueTask<NotificationWorkItem> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}

// Background service
public class NotificationBackgroundService : BackgroundService
{
    private readonly NotificationBackgroundQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _queue.DequeueAsync(stoppingToken);

            // Create scope for scoped dependencies
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            try
            {
                // Resolve and invoke handlers
                var handlerType = typeof(INotificationHandler<>).MakeGenericType(workItem.NotificationType);
                var handlers = scope.ServiceProvider.GetServices(handlerType);

                var tasks = handlers.Cast<object>().Select(handler =>
                {
                    var method = handlerType.GetMethod("HandleAsync");
                    return (Task)method.Invoke(handler, new[] { workItem.Notification, workItem.CancellationToken });
                });

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notification {NotificationType}", workItem.NotificationType.Name);
            }
        }
    }
}

// Updated Mediator.PublishAsync
public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    where TNotification : INotification
{
    if (notification == null) throw new ArgumentNullException(nameof(notification));

    return _notificationQueue.QueueNotificationAsync(notification, cancellationToken).AsTask();
}
```

### Strategy 3: Configurable Publishing Strategy (MediatR-style)
```csharp
public enum NotificationPublishStrategy
{
    SyncStopOnException,        // Current behavior - sequential, stop on first exception
    SyncContinueOnException,    // Sequential, continue on exception, aggregate exceptions
    Async,                       // Parallel with Task.WhenAll - wait for all
    ParallelNoWait              // Fire and forget - don't wait
}

public interface INotificationPublisher
{
    Task PublishAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification;
}

// Fire and forget publisher
public class FireAndForgetNotificationPublisher : INotificationPublisher
{
    public Task PublishAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken) where TNotification : INotification
    {
        _ = Task.Run(async () =>
        {
            var tasks = handlers.Select(h => h.HandleAsync(notification, cancellationToken));
            await Task.WhenAll(tasks);
        }, cancellationToken);

        return Task.CompletedTask;
    }
}
```

## Pros

### Performance Benefits

1. **Reduced Latency:** Calling code continues immediately without waiting for potentially slow handlers
2. **Improved Throughput:** Web requests complete faster, freeing up threads for other work
3. **Better Scalability:** Decouples request processing from background work execution
4. **Non-Blocking:** Ideal for operations that don't need immediate feedback (emails, logging, analytics)

### Operational Benefits

5. **Separation of Concerns:** Request handling separated from side effects
6. **Resilience:** Main request flow not impacted by slow or failing notification handlers
7. **Backpressure Management:** Channel-based approach provides natural flow control
8. **Resource Efficiency:** Background service can process notifications on dedicated threads

### Developer Experience

9. **Simple API:** No changes to handler implementation required
10. **Familiar Pattern:** Fire-and-forget is a well-understood async pattern in .NET
11. **Optional Feature:** Can be opt-in without breaking existing behavior

## Cons

### Exception Handling Challenges

1. **Silent Failures:** Unhandled exceptions in background handlers are invisible to caller
2. **No Error Feedback:** Caller cannot react to handler failures or retry
3. **Debugging Difficulty:** Async execution makes troubleshooting harder
4. **Lost Context:** Exception context (HTTP request, user identity) may be lost

### Reliability Concerns

5. **No Durability:** In-memory queues lose notifications on app restart/crash
6. **Processing Guarantees:** No guarantee notifications will be processed (app pool recycle, crash)
7. **Ordering Issues:** Parallel execution may process notifications out of order
8. **Partial Failures:** Some handlers may succeed while others fail with no rollback

### Scoped Service Complications

9. **Lifetime Management:** Scoped services (DbContext, HttpContext) disposed before background work completes
10. **Service Scope Creation:** Requires manual scope management using `IServiceScopeFactory`
11. **Transaction Boundary Issues:** Background handlers run outside original transaction scope
12. **Identity/Claims Lost:** User context not available in background thread

### Testing Complexity

13. **Non-Deterministic Tests:** Cannot easily assert on handler execution completion
14. **Race Conditions:** Tests may complete before background handlers run
15. **Timing Issues:** Need to add delays or synchronization for verification

### ASP.NET Core Specific Issues

16. **App Pool Recycle:** IIS/Azure App Service may recycle before handlers complete
17. **Not Web-Friendly:** Microsoft explicitly recommends against Task.Run in ASP.NET
18. **Thread Pool Abuse:** Can starve thread pool if many notifications queued
19. **Shutdown Issues:** May not gracefully complete on application shutdown

## Technical Details

### Current Implementation Analysis

The current `PublishAsync` implementation (line 254-269 in Mediator.cs):

```csharp
public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
    where TNotification : INotification
{
    if (notification == null) throw new ArgumentNullException(nameof(notification));

    var handlers = GetServices<INotificationHandler<TNotification>>().ToList();

    if (handlers.Count == 0)
    {
        // No handlers is OK for notifications (fire and forget)
        return;
    }

    var tasks = handlers.Select(handler => handler.HandleAsync(notification, cancellationToken));
    await Task.WhenAll(tasks).ConfigureAwait(false);
}
```

**Key observations:**
- Already uses `Task.WhenAll` for parallel execution
- Waits for ALL handlers to complete before returning
- No exception handling - exceptions bubble to caller
- Comment mentions "fire and forget" but implementation waits

### Implementation Considerations

**1. Exception Handling**
- Must log all exceptions since caller won't see them
- Consider `IRequestExceptionAction<,>` pattern for notification exceptions
- Aggregate exceptions from multiple handlers
- Prevent one handler failure from stopping others

**2. Service Scope Management**
```csharp
// Background handlers need their own scope
using var scope = _serviceProvider.CreateScope();
var scopedServices = scope.ServiceProvider;

// Resolve handlers from scoped provider
var handlers = scopedServices.GetServices<INotificationHandler<TNotification>>();
```

**3. Cancellation Token Handling**
- Original request's cancellation token may fire after caller returns
- Need separate token for background work or copy token state
- Support graceful shutdown with `CancellationToken` from `IHostApplicationLifetime`

**4. Backpressure and Capacity**
```csharp
var options = new BoundedChannelOptions(capacity: 100)
{
    FullMode = BoundedChannelFullMode.Wait  // Apply backpressure
};
```

### Performance Impact

**Memory:**
- Channel-based: ~1KB per queued notification + handler closure
- Task.Run: Minimal overhead but unbounded queue growth

**Latency:**
- Synchronous (current): 10-500ms depending on handler count and slowest handler
- Fire-and-forget: <1ms to queue notification

**Throughput:**
- Current: Limited by slowest notification handler
- Fire-and-forget: Limited by queue capacity and background worker threads

## Dependencies

### New Dependencies

1. **System.Threading.Channels** (built into .NET 6+, no additional package needed)
2. **Microsoft.Extensions.Hosting.Abstractions** (for `IHostedService`, already referenced transitively)
3. **Microsoft.Extensions.Logging.Abstractions** (for logging, likely already referenced)

### Internal Dependencies

4. Extension to `Mediator.cs` core implementation
5. New `INotificationQueue` interface and implementation
6. New `NotificationBackgroundService : BackgroundService`
7. Updates to `ServiceCollectionExtensions.cs` for registration
8. New test fixtures in `Medino.Tests/Events/FireAndForget/`

### Optional Dependencies

9. **External Message Queue** (RabbitMQ, Azure Service Bus) for production durability
10. **MassTransit or NServiceBus** for robust messaging infrastructure
11. **Polly** for retry policies in background handlers

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| **Silent Failures** - Exceptions in background handlers go unnoticed | High | High | Comprehensive logging with structured telemetry; health checks monitoring queue depth |
| **Lost Notifications** - App restart/crash loses in-flight notifications | High | Medium | Document limitation; recommend external queue for critical notifications; implement persistence layer |
| **Service Scope Issues** - Scoped dependencies disposed before use | Medium | High | Provide clear examples of scope creation; document DbContext patterns; add diagnostic warnings |
| **Breaking Change** - Existing code expects synchronous behavior | Low | High | Make fire-and-forget opt-in via configuration or separate method `PublishFireAndForgetAsync` |
| **ASP.NET Thread Pool Starvation** - Too many background tasks | Medium | Medium | Use bounded channels with backpressure; limit concurrent background workers; monitor thread pool metrics |
| **Transaction Boundary Issues** - Background handlers outside transaction | High | High | Document transaction limitations; recommend event sourcing or outbox pattern for transactional scenarios |
| **Testing Difficulties** - Non-deterministic test failures | Medium | Low | Provide testing utilities with synchronization; document testing patterns; consider sync mode for tests |
| **App Pool Recycle** - IIS/Azure kills background work | High | Medium | Document IIS limitations; recommend dedicated worker service or Azure Functions for critical work |
| **Ordering Violations** - Notifications processed out of order | Medium | Low | Document non-deterministic ordering; recommend sequential queue if ordering required |
| **Memory Leaks** - Unbounded queue growth | Low | High | Use bounded channels; implement queue depth monitoring; add circuit breaker for queue full scenarios |

## Effort Estimate

### Development: 4-6 days

- **Day 1-2:** Core Implementation
  - Design `INotificationQueue` and `NotificationBackgroundQueue` with Channel<T>
  - Implement `NotificationBackgroundService : BackgroundService`
  - Add comprehensive error handling and logging
  - Support graceful shutdown with cancellation tokens

- **Day 2-3:** Integration
  - Update `AddMedino()` extension to register background queue and service
  - Add configuration options for queue capacity and behavior
  - Implement opt-in mechanism (configuration or separate API method)
  - Service scope creation and lifetime management

- **Day 4:** Exception Handling
  - Structured logging for background failures
  - Exception aggregation and reporting
  - Dead letter queue for failed notifications (optional)
  - Retry policies with Polly (optional)

- **Day 5-6:** Testing and Refinement
  - Unit tests with synchronization for verification
  - Integration tests with background service
  - Performance benchmarks (sync vs fire-and-forget)
  - Code review and refinements

### Testing: 3-4 days

- **Day 1-2:** Functional Testing
  - Happy path scenarios (notifications successfully queued and processed)
  - Exception handling (handler failures, logging verification)
  - Service scope verification (scoped dependencies work correctly)
  - Cancellation token propagation
  - Graceful shutdown testing

- **Day 2-3:** Performance Testing
  - Benchmarks comparing sync vs fire-and-forget latency
  - Queue capacity and backpressure testing
  - Thread pool impact analysis
  - Memory usage under load
  - Concurrent notification stress testing

- **Day 3-4:** Integration Testing
  - ASP.NET Core integration tests
  - IHostedService lifecycle tests
  - Multiple notification types and handlers
  - Queue full scenarios and recovery

### Documentation: 2-3 days

- **Day 1:** API Documentation
  - XML documentation for new interfaces and classes
  - README.md updates with fire-and-forget examples
  - Configuration guide (queue capacity, bounded vs unbounded)
  - When to use fire-and-forget vs synchronous publishing

- **Day 2:** Best Practices Guide
  - Exception handling patterns in background handlers
  - Service scope management examples
  - Transaction boundary considerations
  - Testing patterns for fire-and-forget scenarios
  - Migration from synchronous to fire-and-forget

- **Day 3:** Migration and Warnings
  - Update MIGRATION.md with MediatR comparison
  - Document limitations and risks prominently
  - Production deployment considerations (IIS, containers, Kubernetes)
  - Alternative approaches (external message queues)
  - Troubleshooting guide

**Total Estimated Effort: 9-13 days**

## Recommendation

### ⚠️ NOT RECOMMENDED for Core Library - RECOMMEND as Optional Extension

Fire-and-forget publishing should **NOT** be the default behavior for Medino's `PublishAsync` method, but **MAY** be valuable as an **optional, well-documented extension** with clear warnings about limitations.

### Why NOT Recommended as Default

1. **Breaking Existing Expectations:** Current `PublishAsync` waits for handlers - changing this breaks caller assumptions about execution completion

2. **Silent Failures Are Dangerous:** The primary problem with fire-and-forget is exception visibility - handlers may fail without caller knowledge, which is problematic for medical software

3. **ASP.NET Anti-Pattern:** Microsoft explicitly warns against Task.Run in ASP.NET Core web applications due to thread pool starvation and app pool recycle issues

4. **Loss of Durability:** In-memory queue loses work on crashes - unacceptable for critical medical notifications

5. **MediatR Creator's Guidance:** Jimmy Bogard (MediatR author) explicitly states: "Don't use MediatR for fire-and-forget. You can use Task-related APIs for that instead, or even messaging/queueing. MediatR was never designed nor intended for that scenario."

6. **Scope Complexity:** Scoped service lifetime issues (DbContext, HttpContext) create subtle bugs that are hard to diagnose

### Alternative Recommendation: External Message Queue

For fire-and-forget scenarios in production medical software, recommend:

1. **Use Proper Message Infrastructure:**
   - RabbitMQ, Azure Service Bus, or AWS SQS for durability
   - MassTransit or NServiceBus for .NET integration
   - Guaranteed delivery, retries, dead letter queues

2. **Keep Medino Simple:**
   - Current synchronous `PublishAsync` is clear, predictable, testable
   - Handlers complete within request scope with proper error handling
   - Transaction boundaries are well-defined

3. **Document Pattern for Async Work:**
   ```csharp
   // In handler, queue to external system
   public class UserCreatedNotificationHandler : INotificationHandler<UserCreatedNotification>
   {
       private readonly IMessageBus _messageBus; // RabbitMQ, Service Bus, etc.

       public async Task HandleAsync(UserCreatedNotification notification, CancellationToken ct)
       {
           // Queue to durable message system for background processing
           await _messageBus.PublishAsync(new SendWelcomeEmailMessage(notification.UserId), ct);
       }
   }
   ```

### If Implemented as Optional Feature

**Only implement if:**
- Clearly documented as experimental/advanced feature
- Opt-in via explicit configuration: `services.AddMedino(cfg => cfg.UseFireAndForgetPublishing())`
- Prominent warnings about limitations in documentation
- NOT recommended for production medical software
- Used only for non-critical scenarios (analytics, logging)

**Success Criteria:**
1. Comprehensive logging of all background exceptions
2. Health checks for queue depth monitoring
3. Graceful degradation on queue full
4. Testing utilities for deterministic verification
5. Zero breaking changes to existing API

## References

### Best Practices and Guidance

- [Fire-and-Forget Methods in C# — Best Practices & Pitfalls | Microsoft Community Hub](https://techcommunity.microsoft.com/blog/educatordeveloperblog/fire-and-forget-methods-in-c-%E2%80%94-best-practices--pitfalls/4299605)
- [Fire and Forget on ASP.NET - Stephen Cleary](https://blog.stephencleary.com/2014/06/fire-and-forget-on-asp-net.html)
- [Fire and Forget Operations | davidfowl/AspNetCoreDiagnosticScenarios](https://deepwiki.com/davidfowl/AspNetCoreDiagnosticScenarios/4.1-fire-and-forget-operations)

### Background Services and Queues

- [Create a Queue Service - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/queue-service)
- [Background tasks with hosted services in ASP.NET Core | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-9.0)
- [Background Processing in .NET: Hosted Services, Queues, and Workers Done Right | Medium](https://medium.com/@orbens/background-processing-in-net-hosted-services-queues-and-workers-done-right-5bf3504f7fcf)
- [Channels - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)

### MediatR and Notification Publishing

- [How To Publish MediatR Notifications In Parallel](https://www.milanjovanovic.tech/blog/how-to-publish-mediatr-notifications-in-parallel)
- [How to Make Notification fire and forget · MediatR Issue #349](https://github.com/jbogard/MediatR/issues/349)
- [How to change MediatR publish strategy · MediatR Discussion #736](https://github.com/jbogard/MediatR/discussions/736)
- [Background Commands with MediatR and Hangfire - CodeOpinion](https://codeopinion.com/background-commands-mediatr-hangfire/)
- [Building a Better MediatR Publisher With Channels](https://www.milanjovanovic.tech/blog/building-a-better-mediatr-publisher-with-channels-and-why-you-shouldnt)

### ASP.NET Core Considerations

- [How to run Background Tasks in ASP.NET - Scott Hanselman](https://www.hanselman.com/blog/how-to-run-background-tasks-in-aspnet)
- [Fire and Forget in ASP.NET Core with dependency alive - Anduin Xue](https://anduin.aiursoft.com/post/2020/10/14/fire-and-forget-in-aspnet-core-with-dependency-alive)
- [Trigger Background Jobs from ASP.NET Core Middleware | ByteCrate](https://bytecrate.dev/aspnet-core-middleware-background-jobs/)

### Message Queue Alternatives

- [Efficiently Handling Asynchronous Request-Reply with an In-Memory Queue and MediatR](https://www.kevinlloyd.net/in-memory-queue-with-mediatr/)
- [Consuming message queues using .NET Core background workers with System.Threading.Channels](https://www.davidguida.net/consuming-message-queues-using-net-core-background-workers-part-4-adding-system-threading-channels/)
