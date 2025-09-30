using NUnit.Framework;

namespace NanoBus.Tests.Requests
{
    [TestFixture]
    public class RequestTests
    {
        [Test]
        public void GivenARequestIsCreated_WhenItIsPublished_ThenTheHandlerIsCalled()
        {
            var e = new TestRequest();

            var response = Mediator.Request(e);
            
            Assert.IsNotNull(response);
        }
    }
}