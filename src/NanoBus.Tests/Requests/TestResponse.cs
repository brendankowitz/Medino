#if NET45
using Nimbus.MessageContracts;
#else
using NanoBus.MessageContracts;
#endif

namespace NanoBus.Tests.Requests
{
    public class TestResponse : IBusResponse
    {
        
    }
}