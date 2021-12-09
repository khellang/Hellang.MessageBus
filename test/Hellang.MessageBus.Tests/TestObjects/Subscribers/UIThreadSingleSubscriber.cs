using Hellang.MessageBus.Tests.TestObjects.Messages;
using Hellang.MessageBus;

namespace Hellang.MessageBus.Tests.TestObjects.Subscribers
{
    public class UIThreadSingleSubscriber : SingleSubscriber
    {
        [HandleOnUiThreadAttribute]
        public override void Handle(TestMessage message)
        {
            base.Handle(message);
        }
    }
}