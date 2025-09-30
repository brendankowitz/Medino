using System.Threading.Tasks;
using NanoBus.MessageContracts;

namespace NanoBus.Handlers
{
    public interface IHandleCommand<TBusCommand> where TBusCommand : IBusCommand
    {
        Task Handle(TBusCommand busCommand);
    }
}