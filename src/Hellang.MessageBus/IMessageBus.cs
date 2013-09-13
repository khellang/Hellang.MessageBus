namespace Hellang.MessageBus
{
    /// <summary>
    /// Enables loosely-coupled publication of and subscription to messages.
    /// </summary>
    public interface IMessageBus : IHideObjectMembers
    {
        /// <summary>
        /// Subscribes the specified target to all messages declared
        /// through implementations of <see cref="IHandle{T}"/>.
        /// </summary>
        /// <param name="target">The target to subscribe for event publication.</param>
        void Subscribe(object target);

        /// <summary>
        /// Unsubscribes the specified target from all events.
        /// </summary>
        /// <param name="target">The target to unsubscribe.</param>
        void Unsubscribe(object target);

        /// <summary>
        /// Publishes a new message of the given message type.
        /// </summary>
        /// <typeparam name="T">The type of message to publish.</typeparam>
        void Publish<T>() where T : new();

        /// <summary>
        /// Publishes the specified message.
        /// </summary>
        /// <typeparam name="T">The type of message to publish</typeparam>
        /// <param name="message">The message.</param>
        void Publish<T>(T message);

        /// <summary>
        /// Clears all subscribers.
        /// </summary>
        void Clear();
    }
}