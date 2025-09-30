NanoBus
=======

Why!?

When you need to start introducing 'Bus' like patterns and concepts to a project that isn't quite ready for the shenanigans of a real bus. You can think of NanoBus as the bus that you use before you get on the Nimbus.

NanoBus' interfaces are highly compatible (read: copied) from Nimbus, as to soften the transition when the time finally arrives. Note that it is the basic interfaces only, all the advanced features are your motivation to move across as soon as you're ready ;)
You can also use this project all the way back to .NET4.0 so you can get a head start in preparing for that right-click-target-4.5 hurdle.

Install:

```
PM> Install-Package NanoBus
```

Configure:

```
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
            .Where(t => t.IsClosedTypeOf(typeof (IHandleCommand<>)))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();
            
        builder.RegisterAssemblyTypes(ThisAssembly)
            .Where(t => t.IsClosedTypeOf(typeof(IHandleRequest<,>)))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        builder.RegisterAssemblyTypes(ThisAssembly)
            .Where(t => t.IsClosedTypeOf(typeof (IHandleMulticastEvent<>)))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();
    }
}
```


And Bus.

```
var myCommand = new TestBusCommand();
Mediator.Send(command);

```
