using Hellang.MessageBus.Tests.TestObjects.Messages;

namespace Hellang.MessageBus.Tests.TestObjects.Subscribers
{
    public class UIThreadSingleSubscriber : SingleSubscriber
    {
        [HandleOnUIThread]
        public override void Handle(TestMessage message)
        {
            base.Handle(message);
        }
    }
}