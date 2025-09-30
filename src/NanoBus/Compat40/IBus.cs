using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NanoBus.MessageContracts;

// ReSharper disable CheckNamespace

namespace NanoBus
// ReSharper restore CheckNamespace
{
    public interface IBus
    {
        Task Send<TBusCommand>(TBusCommand busCommand) where TBusCommand : IBusCommand;

        Task<TResponse> Request<TRequest, TResponse>(IBusRequest<TRequest, TResponse> busRequest)
            where TRequest : IBusRequest<TRequest, TResponse>
            where TResponse : IBusResponse;

        Task Publish<TBusEvent>(TBusEvent busEvent) where TBusEvent : IBusEvent;
    }
}