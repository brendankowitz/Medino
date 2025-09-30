using System.Threading.Tasks;
using NanoBus.MessageContracts;

namespace NanoBus.Handlers
{
    public interface IHandleRequest<TBusRequest, TBusResponse> where TBusRequest : IBusRequest<TBusRequest, TBusResponse>
                                                               where TBusResponse : IBusResponse
    {
        Task<TBusResponse> Handle(TBusRequest request);
    }
}