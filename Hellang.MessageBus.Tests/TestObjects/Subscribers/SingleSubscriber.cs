using System;

using Hellang.MessageBus.Tests.TestObjects.Messages;

namespace Hellang.MessageBus.Tests.TestObjects.Subscribers
{
    public class SingleSubscriber : IHandle<TestMessage>
    {
        public virtual void Handle(TestMessage message)
        {
            ReceivedMessages++;
            LastMessage = message;
            message.LastReceiver = this;
        }

        public void Handle(TestMessage message, string wtf)
        {
            throw new InvalidOperationException("This method should never be called.");
        }

        public TestMessage LastMessage { get; set; }

        public int ReceivedMessages { get; set; }
    }
}