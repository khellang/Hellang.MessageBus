using Hellang.MessageBus.Tests.TestObjects.Messages;

namespace Hellang.MessageBus.Tests.TestObjects.Subscribers {
    public class ExplicitSubscriber : IHandle<TestMessage>
    {
        public int MessageHandleCount { get; set; }

        void IHandle<TestMessage>.Handle(TestMessage message)
        {
            MessageHandleCount++;
        }
         
    }
}