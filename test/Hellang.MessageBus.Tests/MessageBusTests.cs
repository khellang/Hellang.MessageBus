using System;
using System.Runtime.CompilerServices;
using Hellang.MessageBus.Tests.TestObjects;
using Hellang.MessageBus.Tests.TestObjects.Messages;
using Hellang.MessageBus.Tests.TestObjects.Subscribers;
using Xunit;

namespace Hellang.MessageBus.Tests
{
    public class MessageBusTests
    {
        [Fact]
        public void CanSubscribe()
        {
            var bus = new DirectDispatchMessageBus();

            bus.Subscribe(new SingleSubscriber());
        }

        [Fact]
        public void CanSubscribeWithoutHandlerMethod()
        {
            var bus = new DirectDispatchMessageBus();

            bus.Subscribe(new string('s', 10));
        }

        [Fact]
        public void CanSubscribeMultipleTargets()
        {
            var bus = new DirectDispatchMessageBus();

            var target1 = new SingleSubscriber();
            var target2 = new SingleSubscriber();
            bus.Subscribe(target1);
            bus.Subscribe(target2);

            var message = new TestMessage();
            bus.Publish(message);

            Assert.Same(message, target1.LastMessage);
            Assert.Same(message, target2.LastMessage);
        }

        [Fact]
        public void CanUnsubscribe()
        {
            var bus = new DirectDispatchMessageBus();

            var target = new SingleSubscriber();
            bus.Subscribe(target);
            bus.Unsubscribe(target);

            var message = new TestMessage();
            bus.Publish(message);

            Assert.Null(message.LastReceiver);
        }

        [Fact]
        public void UiDispatchWithoutMarshallerIsNotSupported()
        {
            var bus = new MessageBus();

            var target = new UIThreadSingleSubscriber();
            bus.Subscribe(target);

            Assert.Throws<NotSupportedException>(() => bus.Publish<TestMessage>());
        }

        [Fact]
        public void CollectedTargetIsUnsubscribed()
        {
            var bus = new DirectDispatchMessageBus();

            Subscribe(bus);

            GC.Collect();

            var message = new TestMessage();
            bus.Publish(message);

            Assert.Null(message.LastReceiver);
        }

        // This must be in its own non-inlined method in order to work correctly on .NET Core 2.0+
        // See https://github.com/dotnet/coreclr/issues/12847 for more details.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Subscribe(IMessageBus bus)
        {
            bus.Subscribe(new SingleSubscriber());
        }

        [Fact]
        public void CanClearSubscriptions()
        {
            var bus = new DirectDispatchMessageBus();

            var target = new SingleSubscriber();
            bus.Subscribe(target);

            bus.Clear();

            var message = new TestMessage();
            bus.Publish(message);

            Assert.Null(message.LastReceiver);
        }

        [Fact]
        public void SameTargetCanOnlySubscribeOnce()
        {
            var bus = new DirectDispatchMessageBus();

            var target = new SingleSubscriber();
            bus.Subscribe(target);
            bus.Subscribe(target);

            bus.Publish<TestMessage>();

            Assert.Equal(1, target.ReceivedMessages);
        }
        
        [Fact]
        public void MessageIsDispatchedOnCallingThreadByDefault()
        {
            var dispatchedOnUiThread = false;
            var bus = new MessageBus(action =>
            {
                dispatchedOnUiThread = true;
                action();
            });

            var target = new SingleSubscriber();
            bus.Subscribe(target);

            bus.Publish<TestMessage>();

            Assert.False(dispatchedOnUiThread);
        }

        [Fact]
        public void HandleOnUIThreadAttributeIsHonored()
        {
            var dispatchedOnUiThread = false;
            var bus = new MessageBus(action =>
            {
                dispatchedOnUiThread = true;
                action();
            });

            var target = new UIThreadSingleSubscriber();
            bus.Subscribe(target);

            bus.Publish<TestMessage>();

            Assert.True(dispatchedOnUiThread);
        }

        [Fact]
        public void MessagesAreHandledInPolymorphicFashion()
        {
            var bus = new DirectDispatchMessageBus();

            var target = new PolySubscriber();
            bus.Subscribe(target);

            bus.Publish<DerivedTestMessage>();

            Assert.Equal(2, target.MessageHandleCount);
            Assert.Contains(typeof(TestMessage), target.HandledMessageTypes);
            Assert.Contains(typeof(DerivedTestMessage), target.HandledMessageTypes);
        }
      
        [Fact]
        public void MessagesAreHandledByExplicitSubscriber() {
            var bus = new DirectDispatchMessageBus();

            var target = new ExplicitSubscriber();
            bus.Subscribe(target);

            bus.Publish<TestMessage>();

            Assert.Equal(1, target.MessageHandleCount);
        }
    }
     
}