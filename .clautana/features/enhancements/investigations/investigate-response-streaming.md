# Investigation: Response Streaming with IAsyncEnumerable

**Feature:** enhancements
**Date:** 2026-01-03
**Status:** Planned
**Researcher:** investigate-response-streaming-researcher

## Summary

This investigation explores adding response streaming support to Medino using `IAsyncEnumerable<T>`, following the pattern established by MediatR 10.0+. This enhancement would allow handlers to progressively return data instead of materializing entire result sets in memory, improving performance and memory efficiency for large datasets, long-running queries, and real-time data feeds.

## Approach

### Overview

Introduce new streaming request types and handlers alongside existing `IRequest<TResponse>` and `IRequestHandler<TRequest, TResponse>` patterns:

1. **New Interfaces:**
   - `IStreamRequest<TResponse>`: Marker interface for streaming requests
   - `IStreamRequestHandler<TRequest, TResponse>`: Handler that returns `IAsyncEnumerable<TResponse>`

2. **Mediator Extension:**
   - Add `CreateStreamAsync<TResponse>(IStreamRequest<TResponse>, CancellationToken)` method to `IMediator`
   - Returns `IAsyncEnumerable<TResponse>` for progressive data delivery

3. **Pipeline Support:**
   - `IStreamPipelineBehavior<TRequest, TResponse>`: Separate behaviors for stream requests
   - Stream behaviors wrap the entire enumerable, not individual items (cross-cutting concerns)

### Implementation Pattern

```csharp
// Request definition
public record GetLargeDatasetQuery : IStreamRequest<DataItem>;

// Handler implementation
public class GetLargeDatasetQueryHandler
    : IStreamRequestHandler<GetLargeDatasetQuery, DataItem>
{
    public async IAsyncEnumerable<DataItem> HandleAsync(
        GetLargeDatasetQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _database.StreamDataAsync(cancellationToken))
        {
            yield return item;
        }
    }
}

// Usage
await foreach (var item in _mediator.CreateStreamAsync(query, cancellationToken))
{
    // Process each item as it arrives
    Console.WriteLine(item);
}
```

### Architecture Considerations

- **Coexistence:** Streaming requests exist alongside regular requests; handlers choose appropriate pattern
- **Pipeline Execution:** Stream behaviors execute once per stream, not per item
- **Service Registration:** Auto-discovery via assembly scanning in `AddMedino()`
- **Target Framework:** Requires .NET Standard 2.1+ (already met by net8.0/net9.0 targets)

## Pros

### Performance Benefits

1. **Memory Efficiency:** Server doesn't hold entire datasets in memory before responding
2. **Reduced Latency:** Clients start processing data immediately instead of waiting for complete dataset
3. **Backpressure Management:** Natural flow control prevents overwhelming consumers
4. **Scalability:** Handles large datasets without proportional memory growth

### Developer Experience

5. **Simple API:** `yield return` syntax is intuitive and familiar to .NET developers
6. **Native Support:** `IAsyncEnumerable<T>` is first-class in modern .NET (6+)
7. **Cancellation Support:** Built-in `CancellationToken` propagation via `[EnumeratorCancellation]`
8. **MediatR Compatibility:** Familiar pattern for developers migrating from MediatR 10.0+

### Integration Benefits

9. **ASP.NET Core Support:** Native serialization support for streaming JSON responses
10. **EF Core Integration:** Works seamlessly with `AsAsyncEnumerable()` queries
11. **gRPC Streaming:** Natural fit for gRPC server streaming scenarios

## Cons

### Complexity Concerns

1. **Increased API Surface:** Adds new interfaces, behaviors, and mediator methods
2. **Developer Choice Paralysis:** Teams must decide between regular vs. streaming requests
3. **Learning Curve:** Developers need to understand when streaming is appropriate
4. **Documentation Burden:** Requires examples, guidance, and migration notes

### Error Handling Challenges

5. **Exception Handling Limitation:** Cannot use `yield return` inside `catch` blocks (C# compiler restriction CS1626)
6. **Partial Results:** Errors midstream leave consumers with incomplete data
7. **Exception Handler Complexity:** Existing `IRequestExceptionHandler<T>` doesn't fit streaming model well
8. **Rollback Difficulty:** Cannot "un-yield" items already sent to consumer

### Behavioral Constraints

9. **Non-Cooperative Cancellation:** If handler doesn't check `CancellationToken`, iteration continues despite cancellation
10. **Enumeration Semantics:** Multiple enumerations might produce different results or be unsupported
11. **Buffering Risk:** Incorrect usage (e.g., `ToListAsync()`) defeats memory benefits
12. **Backpressure Awareness:** Developers must understand flow control to avoid producer-consumer imbalance

### Testing Complexity

13. **Test Verbosity:** Testing streaming handlers requires `await foreach` loops
14. **Partial Result Verification:** More complex assertions for incremental data
15. **Cancellation Testing:** Must verify proper token handling and cleanup

## Technical Details

### Core Interfaces

```csharp
namespace Medino;

/// <summary>
/// Marker interface for streaming requests that return multiple results
/// </summary>
/// <typeparam name="TResponse">The type of items in the stream</typeparam>
public interface IStreamRequest<out TResponse> { }

/// <summary>
/// Handler for streaming requests
/// </summary>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the streaming request, returning an async enumerable of responses
    /// </summary>
    IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Pipeline behavior for streaming requests
/// </summary>
public interface IStreamPipelineBehavior<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Pipeline handler for streaming. Wraps the entire stream, not individual items.
    /// </summary>
    IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Delegate for the next handler in the streaming pipeline
/// </summary>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<out TResponse>();
```

### IMediator Extension

```csharp
public interface IMediator
{
    // Existing methods...
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;

    // New streaming method
    IAsyncEnumerable<TResponse> CreateStreamAsync<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
```

### Mediator Implementation (Simplified)

```csharp
public async IAsyncEnumerable<TResponse> CreateStreamAsync<TResponse>(
    IStreamRequest<TResponse> request,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    if (request == null) throw new ArgumentNullException(nameof(request));

    var requestType = request.GetType();

    // Resolve handler: IStreamRequestHandler<TRequest, TResponse>
    var handlerType = typeof(IStreamRequestHandler<,>)
        .MakeGenericType(requestType, typeof(TResponse));
    var handler = GetRequiredService(handlerType);

    // Get stream pipeline behaviors
    var behaviorType = typeof(IStreamPipelineBehavior<,>)
        .MakeGenericType(requestType, typeof(TResponse));
    var behaviors = GetServices(behaviorType).ToList();

    var handleMethod = handlerType.GetMethod(nameof(IStreamRequestHandler<IStreamRequest<TResponse>, TResponse>.HandleAsync));

    // Build pipeline (behaviors wrap the entire stream)
    IAsyncEnumerable<TResponse> pipeline = (IAsyncEnumerable<TResponse>)handleMethod.Invoke(
        handler,
        new object[] { request, cancellationToken });

    // Apply behaviors in reverse order
    for (var i = behaviors.Count - 1; i >= 0; i--)
    {
        var behavior = behaviors[i];
        var currentPipeline = pipeline;

        // Invoke behavior's HandleAsync wrapping current pipeline
        var behaviorHandleMethod = behavior.GetType().GetMethod("HandleAsync");
        pipeline = (IAsyncEnumerable<TResponse>)behaviorHandleMethod.Invoke(
            behavior,
            new object[] { request, currentPipeline, cancellationToken });
    }

    // Stream results to caller
    await foreach (var item in pipeline.WithCancellation(cancellationToken))
    {
        yield return item;
    }
}
```

### Exception Handling Strategy

Since `yield return` cannot be in `catch` blocks, exceptions bubble up to consumers:

```csharp
public async IAsyncEnumerable<DataItem> HandleAsync(
    GetDataQuery request,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    // Cannot wrap yield in try-catch with catch clause
    await foreach (var item in _source.GetItemsAsync(cancellationToken))
    {
        // Validation before yielding
        if (item == null)
            throw new InvalidDataException("Null item encountered");

        yield return item;
    }
}

// Consumer handles exceptions
try
{
    await foreach (var item in _mediator.CreateStreamAsync(query))
    {
        Process(item);
    }
}
catch (Exception ex)
{
    // Handle errors (may have partial results already processed)
    _logger.LogError(ex, "Streaming failed");
}
```

### Pipeline Behavior Example

```csharp
public class LoggingStreamBehavior<TRequest, TResponse>
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private readonly ILogger<LoggingStreamBehavior<TRequest, TResponse>> _logger;

    public async IAsyncEnumerable<TResponse> HandleAsync(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting stream for {RequestType}", typeof(TRequest).Name);
        var count = 0;

        await foreach (var item in next().WithCancellation(cancellationToken))
        {
            count++;
            yield return item;
        }

        _logger.LogInformation("Completed stream with {Count} items", count);
    }
}
```

## Dependencies

### Framework Dependencies
- **Target Framework:** .NET Standard 2.1+ (already satisfied by net8.0/net9.0)
- **C# Language Version:** 8.0+ for `IAsyncEnumerable<T>` and `await foreach`
- **Existing Dependency:** `Microsoft.Extensions.DependencyInjection.Abstractions` (no change)

### Internal Dependencies
- Extension to `Mediator.cs` core implementation
- Updates to `ServiceCollectionExtensions.cs` for handler registration
- New test fixtures in `Medino.Tests/Streaming/`

### External Compatibility
- **ASP.NET Core 6+:** Native JSON streaming support
- **Entity Framework Core 5+:** `AsAsyncEnumerable()` support
- **System.Linq.Async:** Optional for LINQ operations on streams

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| **Breaking Changes** - Adding `CreateStreamAsync` to `IMediator` breaks existing implementations | Low | High | Use default interface implementation (C# 8.0) or create `IStreamMediator` extension interface |
| **API Confusion** - Developers unsure when to use streaming vs. regular requests | Medium | Medium | Provide clear documentation with decision tree and performance benchmarks |
| **Memory Leaks** - Unconsumed streams or missing cancellation cause resource leaks | Medium | High | Document proper disposal patterns; add analyzer rules for detecting issues |
| **Partial Results** - Exceptions midstream leave consumers with inconsistent state | High | Medium | Document exception handling patterns; recommend idempotent operations |
| **Performance Overhead** - Reflection and pipeline construction per stream | Low | Low | Similar to existing request handling; benefits outweigh overhead for large datasets |
| **Testing Gaps** - Inadequate testing of streaming edge cases | Medium | Medium | Comprehensive test suite covering cancellation, exceptions, empty streams, single items |
| **Backwards Compatibility** - Migration from MediatR has semantic differences | Low | Medium | Clear migration guide highlighting differences in exception handling |
| **Adoption Resistance** - Developers default to familiar `Task<List<T>>` pattern | Medium | Low | Provide performance benchmarks and migration examples in documentation |

## Effort Estimate

### Development: 5-7 days

- **Day 1-2:** Core interfaces and `CreateStreamAsync` implementation
  - Define `IStreamRequest<T>`, `IStreamRequestHandler<T,R>`, `IStreamPipelineBehavior<T,R>`
  - Implement `Mediator.CreateStreamAsync` with reflection-based handler resolution
  - Pipeline construction for streaming behaviors

- **Day 3:** Registration and DI integration
  - Update `ServiceCollectionExtensions.AddMedino()` to scan for stream handlers
  - Register stream pipeline behaviors
  - Ensure proper service lifetime management

- **Day 4-5:** Comprehensive testing
  - Unit tests for streaming handlers and behaviors
  - Pipeline execution order tests
  - Cancellation token propagation tests
  - Exception handling scenarios
  - Empty stream and single-item edge cases

- **Day 6:** Integration examples
  - ASP.NET Core controller example with JSON streaming
  - EF Core integration example
  - gRPC streaming example (optional)

- **Day 7:** Code review and refinements

### Testing: 3-4 days

- **Day 1:** Test suite development
  - Happy path scenarios
  - Edge cases (empty, single item, cancellation)
  - Exception handling and partial results

- **Day 2:** Performance testing
  - Benchmarks comparing streaming vs. buffered responses
  - Memory profiling with large datasets
  - Throughput testing under load

- **Day 3:** Integration testing
  - ASP.NET Core integration tests
  - DI container scenarios
  - Pipeline behavior composition

- **Day 4:** Test review and additional coverage

### Documentation: 2-3 days

- **Day 1:** API documentation
  - XML documentation comments for all new interfaces
  - README.md updates with streaming examples
  - Decision guide: when to use streaming vs. regular requests

- **Day 2:** Migration guide
  - Update MIGRATION.md with MediatR streaming comparison
  - Breaking changes and upgrade path
  - Common pitfalls and best practices

- **Day 3:** Advanced topics
  - Custom stream behaviors
  - Error handling patterns
  - Performance optimization guide

**Total Estimated Effort: 10-14 days**

## Recommendation

### ✅ RECOMMENDED with Caveats

Response streaming via `IAsyncEnumerable<T>` is a **valuable enhancement** for Medino that aligns with modern .NET practices and MediatR compatibility. However, it should be implemented **thoughtfully** with the following considerations:

### Why Recommend

1. **Clear Use Cases:** Compelling scenarios exist (large datasets, real-time feeds, progressive processing)
2. **Modern .NET Standard:** `IAsyncEnumerable<T>` is first-class in .NET 6+ and widely adopted
3. **MediatR Parity:** Maintains migration path for MediatR 10.0+ users expecting streaming support
4. **Performance Benefits:** Measurable improvements in memory usage and latency for appropriate scenarios
5. **Native Integration:** Excellent ASP.NET Core and EF Core support reduces implementation friction

### Implementation Recommendations

1. **Non-Breaking Addition:**
   - Use default interface methods or extension interface to avoid breaking `IMediator` implementations
   - Keep streaming entirely optional; regular requests remain primary pattern

2. **Clear Documentation:**
   - Decision tree for choosing streaming vs. buffered responses
   - Prominent warning about exception handling limitations
   - Performance benchmarks showing when streaming provides value

3. **Comprehensive Testing:**
   - Extensive test coverage for edge cases (cancellation, exceptions, empty streams)
   - Performance benchmarks included in repository
   - Integration tests with ASP.NET Core

4. **Phased Rollout:**
   - **Phase 1:** Core streaming interfaces and basic implementation (v2.1.0)
   - **Phase 2:** Advanced stream behaviors and utilities (v2.2.0)
   - **Phase 3:** Analyzers for detecting streaming anti-patterns (v2.3.0)

5. **Developer Guidance:**
   - Code analyzer warnings for common mistakes (missing `[EnumeratorCancellation]`, buffering streams)
   - Sample projects demonstrating proper usage
   - Blog post with performance analysis and best practices

### When NOT to Use Streaming

Document that streaming is **not appropriate** for:
- Small datasets (< 100 items) - buffering overhead outweighs benefits
- Operations requiring transactions - partial results on error are problematic
- Scenarios needing result transformation - buffering required anyway
- Simple CRUD operations - unnecessary complexity

### Success Criteria

Consider the feature successful if:
1. Performance benchmarks show >50% memory reduction for datasets >1000 items
2. Zero breaking changes for existing Medino users
3. Documentation rated "clear" by 3+ external reviewers
4. Migration guide tested with real MediatR 10+ codebases
5. Test coverage >90% including edge cases

## References

### Official Documentation
- [Generate and consume async streams - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream)
- [ASP.NET Core 6 and IAsyncEnumerable - Async Streamed JSON vs NDJSON](https://www.tpeczek.com/2021/07/aspnet-core-6-and-iasyncenumerable.html)

### MediatR Implementation
- [MediatR Streaming Support - GitHub Issue #458](https://github.com/jbogard/MediatR/issues/458)
- [C# .NET 8 — Stream Request and Pipeline With MediatR - Medium](https://medium.com/@gabrieletronchin/c-net-8-stream-request-and-pipeline-with-mediatr-a26ddb911b39)
- [MediatR 10.0 Released - Jimmy Bogard](https://www.jimmybogard.com/mediatr-10-0-released/)

### Performance and Best Practices
- [Efficient Data Handling in C# with IAsyncEnumerable - Skerdi Berberi](https://skerdiberberi.com/blog/iasyncenumerable-part2)
- [IEnumerable vs. IAsyncEnumerable in .NET: Streaming vs. Buffering](https://blog.elmah.io/ienumerable-vs-iasyncenumerable-in-net-streaming-vs-buffering/)
- [Streaming Massive Data with IAsyncEnumerable in C# - John Russell](https://jkrussell.dev/blog/streaming-massive-data-iasyncenumerable-csharp/)

### Error Handling and Cancellation
- [Cancellation Tokens with IAsyncEnumerable - Code Maze](https://code-maze.com/csharp-cancellation-tokens-with-iasyncenumerable/)
- [IAsyncEnumerable WithCancellation - Laszlo](https://www.ladeak.net/posts/iasyncenumerable-withcancellation)
- [Async Enumerables with Cancellation - Curiosity is bliss](http://blog.monstuff.com/archives/2019/03/async-enumerables-with-cancellation.html)

### High-Performance Implementations
- [Mediator - High performance implementation using source generators - GitHub](https://github.com/martinothamar/Mediator)
- [Mediator.Net - Simple mediator with pipelines](https://mayuanyang.github.io/Mediator.Net/)

### ASP.NET Core Integration
- [ASP.NET Core in .NET 6 - Async streaming](https://asp.net-hacker.rocks/2021/09/02/aspnetcore6-async-stream.html)
- [ASP.NET Core 6: Streaming JSON responses - Anthony Giretti](https://anthonygiretti.com/2021/09/22/asp-net-core-6-streaming-json-responses-with-iasyncenumerable-example-with-angular/)
- [Async Streaming with EF Core and ASP.NET Core 6 - InfoQ](https://www.infoq.com/news/2021/06/ASP-Net-Core-6-IAsyncEnumerable/)

### Advanced Topics
- [Async Streams and Channels in .NET - Hash Block](https://medium.com/@connect.hashblock/async-streams-and-channels-in-net-real-time-pipelines-that-scale-without-breaking-memory-a8d33e4353e7)
- [How to use IAsyncEnumerable with Blazor Stream Rendering - Khalid Abuhakmeh](https://khalidabuhakmeh.com/how-to-use-iasyncenumerable-with-blazor-stream-rendering)
