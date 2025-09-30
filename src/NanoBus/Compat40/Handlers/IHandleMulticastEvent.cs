using System.Threading.Tasks;
using NanoBus.MessageContracts;

namespace NanoBus.Handlers
{
    public interface IHandleMulticastEvent<TBusEvent> where TBusEvent : IBusEvent
    {
        Task Handle(TBusEvent busEvent);
    }
}