using System;

using Hellang.MessageBus.Tests.TestObjects;
using Hellang.MessageBus.Tests.TestObjects.Messages;
using Hellang.MessageBus.Tests.TestObjects.Subscribers;

using NUnit.Framework;

namespace Hellang.MessageBus.Tests
{
    [TestFixture]
    public class MessageBusTests
    {
        [Test]
        public void CanSubscribeToMessage()
        {
            var bus = new DirectDispatchMessageBus();

            var target = new SingleSubscriber();
            bus.Subscribe(target);

            var message = new TestMessage();
            bus.Publish(message);

            Assert.That(message.LastReceiver, Is.SameAs(target));
        }

        [Test]
        public void CanSubscribeMultipleTargets()
        {
            var bus = new DirectDispatchMessageBus();

            var target1 = new SingleSubscriber();
            var target2 = new SingleSubscriber();
            bus.Subscribe(target1);
            bus.Subscribe(target2);

            var message = new TestMessage();
            bus.Publish(message);

            Assert.That(target1.LastMessage, Is.SameAs(message));
            Assert.That(target2.LastMessage, Is.SameAs(message));
        }

        [Test]
        public void CanUnsubscribe()
        {
            var bus = new DirectDispatchMessageBus();

            var target = new SingleSubscriber();
            bus.Subscribe(target);
            bus.Unsubscribe(target);

            var message = new TestMessage();
            bus.Publish(message);

            Assert.That(message.LastReceiver, Is.Null);
        }

        [Test]
        public void CollectedTargetIsUnsubscribed()
        {
            var bus = new DirectDispatchMessageBus();

            bus.Subscribe(new SingleSubscriber());
            GC.Collect();

            var message = new TestMessage();
            bus.Publish(message);

            Assert.That(message.LastReceiver, Is.Null);
        }
        
        [Test]
        public void MessageIsDispatchedOnCallingThreadByDefault()
        {
            var dispatchedOnUIThread = false;
            var bus = new MessageBus(action =>
                {
                    dispatchedOnUIThread = true;
                    action();
                });

            var target = new SingleSubscriber();
            bus.Subscribe(target);

            bus.Publish<TestMessage>();

            Assert.That(dispatchedOnUIThread, Is.False);
        }

        [Test]
        public void HandleOnUIThreadAttributeIsHonored()
        {
            var dispatchedOnUIThread = false;
            var bus = new MessageBus(action =>
            {
                dispatchedOnUIThread = true;
                action();
            });

            var target = new UIThreadSingleSubscriber();
            bus.Subscribe(target);

            bus.Publish<TestMessage>();

            Assert.That(dispatchedOnUIThread, Is.True);
            Assert.That(dispatchedOnUIThread, Is.True);
        }

        [Test]
        public void MessagesAreHandledInPolymorphicFashion()
        {
            var bus = new DirectDispatchMessageBus();

            var target = new PolySubscriber();
            bus.Subscribe(target);

            bus.Publish<DerivedTestMessage>();

            Assert.That(target.MessageHandleCount, Is.EqualTo(2));
        }
    }
}