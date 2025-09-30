#if NET45
using Nimbus.Handlers;
#else
using NanoBus.Handlers;
#endif

using System.Threading.Tasks;

namespace NanoBus.Tests.Events.Multicast
{
    public class AnotherTestMultiCastEventHandler : IHandleMulticastEvent<TestMultiCastEvent>
    {
        public Task Handle(TestMultiCastEvent busEvent)
        {
            busEvent.SecondHandlerWasCalled = true;
            return Task.Factory.StartNew(() => true);
        }
    }
}