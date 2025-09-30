#if NET45
using Nimbus.Handlers;
#else
using NanoBus.Handlers;
#endif

using System.Threading.Tasks;

namespace NanoBus.Tests.Commands
{
    public class TestCommandHandler : IHandleCommand<TestCommand>
    {
        public Task Handle(TestCommand busCommand)
        {
            busCommand.WasHandled = true;
            return Task.Factory.StartNew(() => true);
        }
    }
}