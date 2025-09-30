using System.Threading.Tasks;
#if NET45
using Nimbus.Handlers;
#else
using NanoBus.Handlers;
#endif

namespace NanoBus.Tests.Requests
{
    public class TestRequestHandler : IHandleRequest<TestRequest,TestResponse>
    {
        public Task<TestResponse> Handle(TestRequest request)
        {
            return Task.Factory.StartNew(() => new TestResponse());
        }
    }
}