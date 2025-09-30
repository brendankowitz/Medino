#if NET45
using Nimbus.MessageContracts;
#else
using NanoBus.MessageContracts;
#endif

namespace NanoBus.Tests.Events.Multicast
{
    public class TestMultiCastEvent : IBusEvent
    {
        public bool WasHandled { get; set; }
        public bool SecondHandlerWasCalled { get; set; }
    }
}