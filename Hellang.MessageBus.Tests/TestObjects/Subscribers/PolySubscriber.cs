using Hellang.MessageBus.Tests.TestObjects.Messages;

namespace Hellang.MessageBus.Tests.TestObjects.Subscribers
{
    public class PolySubscriber : IHandle<TestMessage>, IHandle<DerivedTestMessage>
    {
        public int MessageHandleCount { get; set; }

        public void Handle(TestMessage message)
        {
            MessageHandleCount++;
        }

        public void Handle(DerivedTestMessage message)
        {
            MessageHandleCount++;
        }
    }
}