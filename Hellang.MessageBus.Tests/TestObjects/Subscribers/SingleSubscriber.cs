using Hellang.MessageBus.Tests.TestObjects.Messages;

namespace Hellang.MessageBus.Tests.TestObjects.Subscribers
{
    public class SingleSubscriber : IHandle<TestMessage>
    {
        public virtual void Handle(TestMessage message)
        {
            LastMessage = message;
            message.LastReceiver = this;
        }

        public TestMessage LastMessage { get; set; }
    }
}