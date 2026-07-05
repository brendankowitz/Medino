using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.DependencyInjection;
using Medino.Extensions.DependencyInjection;
using Medino.Tests.Requests;

namespace Medino.Tests.PipelineBehaviors;

/// <summary>
/// Regression tests for a single pipeline behavior class implementing IPipelineBehavior&lt;,&gt;
/// for more than one closed-generic request type. Resolving HandleAsync via the behavior's
/// concrete Type (rather than the specific interface it was resolved for) previously threw
/// AmbiguousMatchException, since the concrete type declares more than one public HandleAsync
/// overload in that scenario.
/// </summary>
public class MultiInterfacePipelineBehaviorTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly MultiRequestTypeBehavior _behavior;

    public MultiInterfacePipelineBehaviorTests()
    {
        var services = new ServiceCollection();

        _behavior = new MultiRequestTypeBehavior();
        services.AddSingleton(_behavior);
        services.AddSingleton<IPipelineBehavior<TestRequest, TestResponse>>(sp => sp.GetRequiredService<MultiRequestTypeBehavior>());
        services.AddSingleton<IPipelineBehavior<AnotherTestRequest, TestResponse>>(sp => sp.GetRequiredService<MultiRequestTypeBehavior>());

        services.AddMedino(typeof(MultiInterfacePipelineBehaviorTests).Assembly);
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }

    [Fact]
    public async Task GivenBehaviorImplementingMultipleClosedGenericInterfaces_WhenSendingFirstRequestType_ThenHandleAsyncIsInvokedWithoutAmbiguity()
    {
        var response = await _mediator.SendAsync(new TestRequest());

        Assert.NotNull(response);
        Assert.Equal("Success", response.Message);
        Assert.Equal(1, _behavior.TestRequestHandleCount);
        Assert.Equal(0, _behavior.AnotherTestRequestHandleCount);
    }

    [Fact]
    public async Task GivenBehaviorImplementingMultipleClosedGenericInterfaces_WhenSendingSecondRequestType_ThenHandleAsyncIsInvokedWithoutAmbiguity()
    {
        var response = await _mediator.SendAsync(new AnotherTestRequest());

        Assert.NotNull(response);
        Assert.Equal("Success", response.Message);
        Assert.Equal(0, _behavior.TestRequestHandleCount);
        Assert.Equal(1, _behavior.AnotherTestRequestHandleCount);
    }
}

/// <summary>
/// Regression tests for the context-behavior path of the same fix: a single class implementing
/// IContextPipelineBehavior&lt;,&gt; for more than one closed-generic request type. Like the regular
/// behavior case, resolving HandleAsync from the concrete Type previously threw AmbiguousMatchException.
/// </summary>
public class MultiInterfaceContextPipelineBehaviorTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly MultiRequestTypeContextBehavior _behavior;

    public MultiInterfaceContextPipelineBehaviorTests()
    {
        var services = new ServiceCollection();

        _behavior = new MultiRequestTypeContextBehavior();
        services.AddSingleton(_behavior);
        services.AddSingleton<IContextPipelineBehavior<TestRequest, TestResponse>>(sp => sp.GetRequiredService<MultiRequestTypeContextBehavior>());
        services.AddSingleton<IContextPipelineBehavior<AnotherTestRequest, TestResponse>>(sp => sp.GetRequiredService<MultiRequestTypeContextBehavior>());

        services.AddMedino(typeof(MultiInterfaceContextPipelineBehaviorTests).Assembly);
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }

    [Fact]
    public async Task GivenContextBehaviorImplementingMultipleClosedGenericInterfaces_WhenSendingFirstRequestType_ThenHandleAsyncIsInvokedWithoutAmbiguity()
    {
        var response = await _mediator.SendAsync(new TestRequest());

        Assert.NotNull(response);
        Assert.Equal("Success", response.Message);
        Assert.Equal(1, _behavior.TestRequestHandleCount);
        Assert.Equal(0, _behavior.AnotherTestRequestHandleCount);
    }

    [Fact]
    public async Task GivenContextBehaviorImplementingMultipleClosedGenericInterfaces_WhenSendingSecondRequestType_ThenHandleAsyncIsInvokedWithoutAmbiguity()
    {
        var response = await _mediator.SendAsync(new AnotherTestRequest());

        Assert.NotNull(response);
        Assert.Equal("Success", response.Message);
        Assert.Equal(0, _behavior.TestRequestHandleCount);
        Assert.Equal(1, _behavior.AnotherTestRequestHandleCount);
    }
}

public record AnotherTestRequest : IRequest<TestResponse>;

public class AnotherTestRequestHandler : IRequestHandler<AnotherTestRequest, TestResponse>
{
    public Task<TestResponse> HandleAsync(AnotherTestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new TestResponse());
    }
}

/// <summary>
/// A single behavior implementing IPipelineBehavior&lt;,&gt; for two different request types
/// sharing the same response type - mirroring real-world usages where one behavior class
/// applies shared logic (e.g. search parameter bookkeeping) across a Create and an Upsert request.
/// </summary>
public class MultiRequestTypeBehavior : IPipelineBehavior<TestRequest, TestResponse>, IPipelineBehavior<AnotherTestRequest, TestResponse>
{
    public int TestRequestHandleCount { get; private set; }

    public int AnotherTestRequestHandleCount { get; private set; }

    public Task<TestResponse> HandleAsync(TestRequest request, RequestHandlerDelegate<TestResponse> next, CancellationToken cancellationToken)
    {
        TestRequestHandleCount++;
        return next();
    }

    public Task<TestResponse> HandleAsync(AnotherTestRequest request, RequestHandlerDelegate<TestResponse> next, CancellationToken cancellationToken)
    {
        AnotherTestRequestHandleCount++;
        return next();
    }
}

/// <summary>
/// A single context behavior implementing IContextPipelineBehavior&lt;,&gt; for two different request
/// types sharing the same response type - the context-behavior analog of MultiRequestTypeBehavior.
/// </summary>
public class MultiRequestTypeContextBehavior : IContextPipelineBehavior<TestRequest, TestResponse>, IContextPipelineBehavior<AnotherTestRequest, TestResponse>
{
    public int TestRequestHandleCount { get; private set; }

    public int AnotherTestRequestHandleCount { get; private set; }

    public Task<TestResponse> HandleAsync(PipelineContext<TestRequest> context, RequestHandlerDelegate<TestResponse> next, CancellationToken cancellationToken)
    {
        TestRequestHandleCount++;
        return next();
    }

    public Task<TestResponse> HandleAsync(PipelineContext<AnotherTestRequest> context, RequestHandlerDelegate<TestResponse> next, CancellationToken cancellationToken)
    {
        AnotherTestRequestHandleCount++;
        return next();
    }
}

/// <summary>
/// Verifies the merged behavior list (request-typed + object-based) works when a multi-interface
/// behavior participates alongside an object-based IPipelineBehavior&lt;object, TResponse&gt; in the
/// same pipeline. Each behavior is paired with its own resolved HandleAsync MethodInfo, so both must
/// dispatch correctly and chain through to the handler.
/// </summary>
public class MultiInterfaceWithObjectBehaviorTests
{
    [Fact]
    public async Task GivenMultiInterfaceBehaviorAndObjectBehavior_WhenSending_ThenBothParticipateAndChainToHandler()
    {
        var services = new ServiceCollection();

        var multiBehavior = new MultiRequestTypeBehavior();
        services.AddSingleton(multiBehavior);
        services.AddSingleton<IPipelineBehavior<TestRequest, TestResponse>>(sp => sp.GetRequiredService<MultiRequestTypeBehavior>());

        var objectBehavior = new TestResponseLoggingBehavior();
        services.AddSingleton<IPipelineBehavior<object, TestResponse>>(objectBehavior);

        services.AddMedino(typeof(MultiInterfacePipelineBehaviorTests).Assembly);
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var response = await mediator.SendAsync(new TestRequest());

        // Response chained all the way through the handler (not short-circuited).
        Assert.Equal("Success", response.Message);
        // The multi-interface behavior dispatched to the correct overload exactly once.
        Assert.Equal(1, multiBehavior.TestRequestHandleCount);
        Assert.Equal(0, multiBehavior.AnotherTestRequestHandleCount);
        // The object-based behavior wrapped the pipeline; "After" proves next() completed through the handler.
        Assert.Contains("Before: TestRequest", objectBehavior.Logs);
        Assert.Contains("After: TestRequest", objectBehavior.Logs);
    }
}

/// <summary>
/// Pins the fail-fast contract for IContextPipelineBehavior&lt;object, TResponse&gt;: it can never be
/// invoked (PipelineContext&lt;T&gt; is invariant, so a PipelineContext&lt;TRequest&gt; is not assignable
/// to the PipelineContext&lt;object&gt; parameter), so AddMedino must reject it at registration rather
/// than wiring up a behavior that would silently never run. The offending type is emitted into a
/// dynamic assembly so it does not poison every other test's AddMedino scan of the test assembly.
/// </summary>
public class ObjectContextBehaviorRegistrationTests
{
    [Fact]
    public void GivenContextBehaviorTypedToObject_WhenAddMedinoScansIt_ThenThrowsAtRegistration()
    {
        var assembly = EmitAssemblyWithObjectContextBehavior();
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() => services.AddMedino(assembly));

        Assert.Contains("IContextPipelineBehavior<object", ex.Message);
        Assert.Contains("PipelineContext<T> is invariant", ex.Message);
    }

    private static Assembly EmitAssemblyWithObjectContextBehavior()
    {
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Medino.Tests.Dynamic.ObjectContextBehavior"),
            AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("Main");

        var typeBuilder = moduleBuilder.DefineType("BadObjectContextBehavior", TypeAttributes.Public | TypeAttributes.Class);
        var interfaceType = typeof(IContextPipelineBehavior<,>).MakeGenericType(typeof(object), typeof(TestResponse));
        typeBuilder.AddInterfaceImplementation(interfaceType);

        var interfaceMethod = interfaceType.GetMethod(nameof(IContextPipelineBehavior<object, TestResponse>.HandleAsync))!;
        var parameterTypes = interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray();
        var methodBuilder = typeBuilder.DefineMethod(
            nameof(IContextPipelineBehavior<object, TestResponse>.HandleAsync),
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            interfaceMethod.ReturnType,
            parameterTypes);

        // Body is never executed - registration throws before any instance is created. Return null to
        // satisfy the verifier (Task<TResponse> is a reference type).
        var il = methodBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ret);
        typeBuilder.DefineMethodOverride(methodBuilder, interfaceMethod);

        typeBuilder.CreateType();
        return assemblyBuilder;
    }
}
