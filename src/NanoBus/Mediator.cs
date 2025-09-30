using System.Threading.Tasks;

#if NET45
using Nimbus;
using Nimbus.MessageContracts;
#else
using NanoBus;
using NanoBus.MessageContracts;
#endif

namespace NanoBus
{
    public static class Mediator
    {
        private static IBus Instance { get; set; }

        public static void SetInstance(IBus bus)
        {
            Instance = bus;
        }

        public static Task SendAsync<TCommand>(TCommand busCommand) where TCommand : IBusCommand
        {
            return Instance.Send(busCommand);
        }

        public static Task PublishAsync<TBusEvent>(TBusEvent busEvent) where TBusEvent : IBusEvent
        {
            return Instance.Publish(busEvent);
        }

        public static Task<TResponse> RequestAsync<TRequest, TResponse>(IBusRequest<TRequest, TResponse> busRequest)
            where TRequest : IBusRequest<TRequest, TResponse>
            where TResponse : IBusResponse
        {
            return Instance.Request(busRequest);
        }

        public static void Send<TCommand>(TCommand busCommand) where TCommand : IBusCommand
        {
            Instance.Send(busCommand).Wait();
        }

        public static void Publish<TBusEvent>(TBusEvent busEvent) where TBusEvent : IBusEvent
        {
            Instance.Publish(busEvent).Wait();
        }

        public static TResponse Request<TRequest, TResponse>(IBusRequest<TRequest, TResponse> busRequest)
            where TRequest : IBusRequest<TRequest, TResponse>
            where TResponse : IBusResponse
        {
            var response = Instance.Request(busRequest);
            response.Wait();
            return response.Result;
        }
    }
}