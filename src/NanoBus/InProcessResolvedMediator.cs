using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;

#if NET45
using Nimbus;
using Nimbus.Handlers;
using Nimbus.MessageContracts;
using Nimbus.MessageContracts.Exceptions;
#else
using NanoBus;
using NanoBus.Handlers;
using NanoBus.MessageContracts;
using NanoBus.MessageContracts.Exceptions;
#endif

namespace NanoBus
{
    public class InProcessScopedMediator : IBus
    {
        private readonly Func<LifetimeContext> _contextResolver;

        public InProcessScopedMediator(Func<ILifetimeScope> lifetimeScope, bool ownsLifetime)
        {
            _contextResolver = () => new LifetimeContext(lifetimeScope(), ownsLifetime);
        }

        public Task Send<TBusCommand>(TBusCommand busCommand) where TBusCommand : IBusCommand
        {
            using (var context = _contextResolver())
            {
                var handler = context.LifetimeScope.Resolve<IHandleCommand<TBusCommand>>();
                return handler.Handle(busCommand);
            }
        }

        public Task<TResponse> Request<TRequest, TResponse>(IBusRequest<TRequest, TResponse> busRequest)
            where TRequest : IBusRequest<TRequest, TResponse>
            where TResponse : IBusResponse
        {
            using (var context = _contextResolver())
            {
                var handler = context.LifetimeScope.Resolve<IHandleRequest<TRequest, TResponse>>();
                return handler.Handle((TRequest)busRequest);
            }
        }

        public Task Publish<TBusEvent>(TBusEvent busEvent) where TBusEvent : IBusEvent
        {
            using (var context = _contextResolver())
            {
                Task[] tasks;

                var competingEvent = context.LifetimeScope.Resolve<IEnumerable<IHandleCompetingEvent<TBusEvent>>>().ToArray();

                if (competingEvent.Any())
                {
                    if (competingEvent.Count() != 1)
                        throw new BusException(string.Format("Exactly one event handler should be registered when using the in-process bus for the event '{0}'",
                            typeof(TBusEvent).Name));

                    tasks = competingEvent
                        .Select(h => h.Handle(busEvent))
                        .ToArray();
                }
                else
                {
                    var handlers = context.LifetimeScope.Resolve<IEnumerable<IHandleMulticastEvent<TBusEvent>>>();

                    tasks = handlers
                        .Select(h => h.Handle(busEvent))
                        .ToArray();
                }

                if (tasks.Any() == false)
                    throw new BusException(string.Format("No event handlers are registered for '{0}'", typeof (TBusEvent).Name));

#if NET45
                return Task.WhenAll(tasks);
#else
                Task.WaitAll(tasks);
                return Task.Factory.StartNew(() => true);
#endif
            }
        }

#if NET45
        public Task SendAt<TBusCommand>(TBusCommand busCommand, DateTimeOffset deliveryTime) where TBusCommand : IBusCommand
        {
            throw new NotImplementedException("Only available after upgrading to Nimbus");
        }

        public Task<TResponse> Request<TRequest, TResponse>(IBusRequest<TRequest, TResponse> busRequest, TimeSpan timeout)
            where TRequest : IBusRequest<TRequest, TResponse>
            where TResponse : IBusResponse
        {
            throw new NotImplementedException("Only available after upgrading to Nimbus");
        }

        public Task<IEnumerable<TResponse>> MulticastRequest<TRequest, TResponse>(IBusRequest<TRequest, TResponse> busRequest, TimeSpan timeout)
            where TRequest : IBusRequest<TRequest, TResponse>
            where TResponse : IBusResponse
        {
            throw new NotImplementedException("Only available after upgrading to Nimbus");
        }

        public IDeadLetterQueues DeadLetterQueues { get { throw new NotImplementedException("Only available after upgrading to Nimbus"); } }
#endif

        class LifetimeContext : IDisposable
        {
            private readonly bool _shouldDispose;
            public ILifetimeScope LifetimeScope { get; private set; }

            public LifetimeContext(ILifetimeScope lifetimeScope, bool shouldDispose)
            {
                LifetimeScope = lifetimeScope;
                _shouldDispose = shouldDispose;
            }

            public void Dispose()
            {
                if (_shouldDispose)
                    LifetimeScope.Dispose();
            }
        }
    }
}