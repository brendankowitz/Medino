using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;

namespace Medino.Tests.PipelineBehaviors;

public class ContextPipelineBehaviorTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;

    public ContextPipelineBehaviorTests()
    {
        var services = new ServiceCollection();

        // Register behaviors as singleton so we can inspect state
        services.AddSingleton<IContextPipelineBehavior<TenantRequest, TenantResponse>, TenantEnrichmentBehavior>();
        services.AddSingleton<IContextPipelineBehavior<SanitizationRequest, SanitizationResponse>, SanitizationBehavior>();
        services.AddSingleton<IContextPipelineBehavior<MetadataRequest, string>, CorrelationIdBehavior>();
        services.AddSingleton<IContextPipelineBehavior<CombinedRequest, string>, FirstTransformBehavior>();
        services.AddSingleton<IPipelineBehavior<object, string>, ObservingBehavior>();

        services.AddMedino(typeof(ContextPipelineBehaviorTests).Assembly);
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task RequestTransformBehavior_ShouldTransformRequest()
    {
        // Arrange
        var behavior = _serviceProvider.GetServices<IContextPipelineBehavior<TenantRequest, TenantResponse>>()
            .OfType<TenantEnrichmentBehavior>()
            .First();
        var request = new TenantRequest { UserId = 123 };

        // Act
        var response = await _mediator.SendAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("TENANT-123", response.TenantId);
        Assert.True(behavior.WasTransformed);
    }

    [Fact]
    public async Task RequestEnrichmentBehavior_ShouldAddMetadata()
    {
        // Arrange
        var behavior = _serviceProvider.GetServices<IContextPipelineBehavior<MetadataRequest, string>>()
            .OfType<CorrelationIdBehavior>()
            .First();
        var request = new MetadataRequest { Value = "test" };

        // Act
        var response = await _mediator.SendAsync(request);

        // Assert
        Assert.Equal("Handled: test", response);
        Assert.NotNull(behavior.LastCorrelationId);
        Assert.NotEmpty(behavior.LastCorrelationId);
    }

    [Fact]
    public async Task ContextBehavior_ShouldSanitizeInput()
    {
        // Arrange
        var behavior = _serviceProvider.GetServices<IContextPipelineBehavior<SanitizationRequest, SanitizationResponse>>()
            .OfType<SanitizationBehavior>()
            .First();
        var request = new SanitizationRequest
        {
            Email = "  USER@EXAMPLE.COM  ",
            Name = "  John Doe  "
        };

        // Act
        var response = await _mediator.SendAsync(request);

        // Assert
        Assert.Equal("user@example.com", response.ProcessedEmail);
        Assert.Equal("John Doe", response.ProcessedName);
        Assert.True(behavior.WasSanitized);
    }

    [Fact]
    public async Task ContextAndRegularBehaviors_ShouldExecuteInCorrectOrder()
    {
        // Arrange
        var contextBehavior = _serviceProvider.GetServices<IContextPipelineBehavior<CombinedRequest, string>>()
            .OfType<FirstTransformBehavior>()
            .First();
        var regularBehavior = _serviceProvider.GetServices<IPipelineBehavior<object, string>>()
            .OfType<ObservingBehavior>()
            .First();

        var request = new CombinedRequest { Value = "original" };

        // Act
        var response = await _mediator.SendAsync(request);

        // Assert
        // Context behavior transforms first (uppercase of "original")
        Assert.Equal("ORIGINAL", contextBehavior.TransformedValue);
        // Regular behavior observes the transformed value
        Assert.Equal("ORIGINAL", regularBehavior.ObservedValue);
        // Handler receives transformed value
        Assert.Equal("Handled: ORIGINAL", response);
    }
}

// Test request and handlers for tenant enrichment
public record TenantRequest : IRequest<TenantResponse>
{
    public int UserId { get; init; }
    public string? TenantId { get; init; }
}

public record TenantResponse
{
    public string? TenantId { get; init; }
}

public class TenantRequestHandler : IRequestHandler<TenantRequest, TenantResponse>
{
    public Task<TenantResponse> HandleAsync(TenantRequest request, CancellationToken cancellationToken)
    {
        // Handler uses the enriched tenant ID from the transformed request
        return Task.FromResult(new TenantResponse { TenantId = request.TenantId });
    }
}

public class TenantEnrichmentBehavior : RequestTransformBehavior<TenantRequest, TenantResponse>
{
    public bool WasTransformed { get; private set; }

    protected override Task<TenantRequest> TransformAsync(TenantRequest request, CancellationToken cancellationToken)
    {
        WasTransformed = true;
        // In real scenario, would fetch from ITenantProvider
        var tenantId = $"TENANT-{request.UserId}";
        return Task.FromResult(request with { TenantId = tenantId });
    }
}

// Test request and handlers for metadata enrichment
public record MetadataRequest : IRequest<string>
{
    public string Value { get; init; } = string.Empty;
}

public class MetadataRequestHandler : IRequestHandler<MetadataRequest, string>
{
    public Task<string> HandleAsync(MetadataRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Handled: {request.Value}");
    }
}

public class CorrelationIdBehavior : RequestEnrichmentBehavior<MetadataRequest, string>
{
    public string? LastCorrelationId { get; private set; }

    protected override Task EnrichAsync(PipelineContext<MetadataRequest> context, CancellationToken cancellationToken)
    {
        LastCorrelationId = Guid.NewGuid().ToString();
        context.SetMetadata("CorrelationId", LastCorrelationId);
        context.SetMetadata("Timestamp", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}

// Test request and handlers for sanitization
public record SanitizationRequest : IRequest<SanitizationResponse>
{
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public record SanitizationResponse
{
    public string ProcessedEmail { get; init; } = string.Empty;
    public string ProcessedName { get; init; } = string.Empty;
}

public class SanitizationRequestHandler : IRequestHandler<SanitizationRequest, SanitizationResponse>
{
    public Task<SanitizationResponse> HandleAsync(SanitizationRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new SanitizationResponse
        {
            ProcessedEmail = request.Email,
            ProcessedName = request.Name
        });
    }
}

public class SanitizationBehavior : IContextPipelineBehavior<SanitizationRequest, SanitizationResponse>
{
    public bool WasSanitized { get; private set; }

    public async Task<SanitizationResponse> HandleAsync(
        PipelineContext<SanitizationRequest> context,
        RequestHandlerDelegate<SanitizationResponse> next,
        CancellationToken cancellationToken)
    {
        // Sanitize the request
        var sanitized = context.Request with
        {
            Email = context.Request.Email.Trim().ToLowerInvariant(),
            Name = context.Request.Name.Trim()
        };

        context.Request = sanitized;
        context.SetMetadata("WasSanitized", true);
        WasSanitized = true;

        return await next();
    }
}

// Test request and handlers for combined execution order
public record CombinedRequest : IRequest<string>
{
    public string Value { get; init; } = string.Empty;
}

public class CombinedRequestHandler : IRequestHandler<CombinedRequest, string>
{
    public Task<string> HandleAsync(CombinedRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Handled: {request.Value}");
    }
}

public class FirstTransformBehavior : RequestTransformBehavior<CombinedRequest, string>
{
    public string? TransformedValue { get; private set; }

    protected override Task<CombinedRequest> TransformAsync(CombinedRequest request, CancellationToken cancellationToken)
    {
        TransformedValue = request.Value.ToUpperInvariant();
        return Task.FromResult(request with { Value = TransformedValue });
    }
}

public class ObservingBehavior : IPipelineBehavior<object, string>
{
    public string? ObservedValue { get; private set; }

    public async Task<string> HandleAsync(object request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
    {
        if (request is CombinedRequest combined)
        {
            ObservedValue = combined.Value;
        }
        return await next();
    }
}