using System.Threading.Tasks;
using NanoBus.MessageContracts;

namespace NanoBus.Handlers
{
    public interface IHandleCompetingEvent<TBusEvent> where TBusEvent : IBusEvent
    {
        Task Handle(TBusEvent busEvent);
    }
}