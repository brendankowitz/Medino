using Autofac;
using NanoBus;
using NUnit.Framework;

#if NET45
using Nimbus.Handlers;
#else
using NanoBus.Handlers;
#endif

[SetUpFixture]
// ReSharper disable once CheckNamespace
public class GlobalSetup
{
    public IContainer Container { get; set; }

    [SetUp]
    public void ShowSomeTrace()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<BusModule>();
        Container = builder.Build();
    }
}

public class BusModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<InProcessMediator>()
            .AsImplementedInterfaces()
            .AutoActivate()
            .OnActivated(c => Mediator.SetInstance(c.Instance))
            .SingleInstance();

        builder.RegisterAssemblyTypes(ThisAssembly)
            .Where(t => t.IsClosedTypeOf(typeof(IHandleCommand<>)))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        builder.RegisterAssemblyTypes(ThisAssembly)
            .Where(t => t.IsClosedTypeOf(typeof(IHandleRequest<,>)))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        builder.RegisterAssemblyTypes(ThisAssembly)
            .Where(t => t.IsClosedTypeOf(typeof(IHandleMulticastEvent<>)))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();
    }
}