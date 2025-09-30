#if NET45
using Nimbus.MessageContracts;
#else
using NanoBus.MessageContracts;
#endif

namespace NanoBus.Tests.Commands
{
    public class TestCommand : IBusCommand
    {
        public bool WasHandled { get; set; }
    }
}