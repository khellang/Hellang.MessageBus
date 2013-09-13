namespace Hellang.MessageBus.Tests.TestObjects
{
    public class DirectDispatchMessageBus : MessageBus
    {
        public DirectDispatchMessageBus() : base(action => action()) { }
    }
}