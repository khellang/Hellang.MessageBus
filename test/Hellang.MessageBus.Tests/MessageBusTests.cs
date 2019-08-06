using System;
using System.Runtime.CompilerServices;
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
        public void CanSubscribe()
        {
            var bus = new DirectDispatchMessageBus();

            Assert.DoesNotThrow(() => bus.Subscribe(new SingleSubscriber()));
        }

        [Test]
        public void CanSubscribeWithoutHandlerMethod()
        {
            var bus = new DirectDispatchMessageBus();

            Assert.DoesNotThrow(() => bus.Subscribe(new string('s', 10)));
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

            Subscribe(bus);

            GC.Collect();

            var message = new TestMessage();
            bus.Publish(message);

            Assert.That(message.LastReceiver, Is.Null);
        }

        // This must be in its own non-inlined method in order to work correctly on .NET Core 2.0+
        // See https://github.com/dotnet/coreclr/issues/12847 for more details.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Subscribe(IMessageBus bus)
        {
            bus.Subscribe(new SingleSubscriber());
        }

        [Test]
        public void CanClearSubscriptions()
        {
            var bus = new DirectDispatchMessageBus();

            var target = new SingleSubscriber();
            bus.Subscribe(target);

            bus.Clear();

            var message = new TestMessage();
            bus.Publish(message);

            Assert.That(message.LastReceiver, Is.Null);
        }

        [Test]
        public void SameTargetCanOnlySubscribeOnce()
        {
            var bus = new DirectDispatchMessageBus();

            var target = new SingleSubscriber();
            bus.Subscribe(target);
            bus.Subscribe(target);

            bus.Publish<TestMessage>();

            Assert.That(target.ReceivedMessages, Is.EqualTo(1));
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
        }

        [Test]
        public void MessagesAreHandledInPolymorphicFashion()
        {
            var bus = new DirectDispatchMessageBus();

            var target = new PolySubscriber();
            bus.Subscribe(target);

            bus.Publish<DerivedTestMessage>();

            Assert.That(target.MessageHandleCount, Is.EqualTo(2));
            Assert.That(target.HandledMessageTypes.Contains(typeof(TestMessage)));
            Assert.That(target.HandledMessageTypes.Contains(typeof(DerivedTestMessage)));
        }
    }
}