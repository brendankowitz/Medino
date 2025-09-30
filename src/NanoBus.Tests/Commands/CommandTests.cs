using NanoBus.Tests.Events;
using NUnit.Framework;

namespace NanoBus.Tests.Commands
{
    [TestFixture]
    public class CommandTests
    {
        [Test]
        public void GivenACommandIsCreated_WhenItIsPublished_ThenTheHandlerIsCalled()
        {
            var e = new TestCommand();

            Mediator.Send(e);
            
            Assert.IsTrue(e.WasHandled);
        }
    }
}