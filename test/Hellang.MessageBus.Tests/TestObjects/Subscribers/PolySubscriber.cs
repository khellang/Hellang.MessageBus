using System;
using System.Collections.Generic;

using Hellang.MessageBus.Tests.TestObjects.Messages;

namespace Hellang.MessageBus.Tests.TestObjects.Subscribers
{
    public class PolySubscriber : IHandle<TestMessage>, IHandle<DerivedTestMessage>
    {
        public List<Type> HandledMessageTypes = new List<Type>();

        public int MessageHandleCount { get; set; }

        public void Handle(TestMessage message)
        {
            MessageHandleCount++;
            HandledMessageTypes.Add(typeof(TestMessage));
        }

        public void Handle(DerivedTestMessage message)
        {
            MessageHandleCount++;
            HandledMessageTypes.Add(typeof(DerivedTestMessage));
        }
    }
}