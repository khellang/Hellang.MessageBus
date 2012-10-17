using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hellang.MessageBus
{
    /// <summary>
    /// A marker interface for classes that can subscribe to messages.
    /// </summary>
    public interface IHandle { }

    /// <summary>
    /// Denotes a class which can handle a particular type of message.
    /// </summary>
    /// <typeparam name="T">The type of message to handle.</typeparam>
    public interface IHandle<in T> : IHandle
    {
        /// <summary>
        /// Handles the given message.
        /// </summary>
        /// <param name="message">The message.</param>
        void Handle(T message);
    }

    /// <summary>
    /// Attribute for specifying that the message
    /// handling should be done on the UI thread.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class HandleOnUIThreadAttribute : Attribute { }

    /// <summary>
    /// Enables loosely-coupled publication of and subscription to messages.
    /// </summary>
    public class MessageBus : IMessageBus
    {
        protected static Action<Action> UIThreadMarshaller;

        protected readonly List<Subscriber> Subscribers = new List<Subscriber>();
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBus" /> class.
        /// </summary>
        /// <param name="uiThreadMarshaller"> </param>
        public MessageBus(Action<Action> uiThreadMarshaller)
        {
            UIThreadMarshaller = uiThreadMarshaller;
        }

        /// <summary>
        /// Subscribes the specified target to all messages declared
        /// through implementations of <see cref="IHandle{T}" />.
        /// </summary>
        /// <param name="target">The target to subscribe for event publication.</param>
        public virtual void Subscribe(object target)
        {
            WhileLocked(() =>
                {
                    if (Subscribers.Any(s => s.Matches(target))) return;
                    Subscribers.Add(CreateSubscriberForTarget(target));
                });
        }

        /// <summary>
        /// Unsubscribes the specified target from all events.
        /// </summary>
        /// <param name="target">The target to unsubscribe.</param>
        public virtual void Unsubscribe(object target)
        {
            WhileLocked(() => Subscribers.RemoveAll(s => s.Matches(target)));
        }

        /// <summary>
        /// Publishes a new message of the given message type.
        /// </summary>
        /// <typeparam name="T">The type of message to publish.</typeparam>
        public virtual void Publish<T>() where T : new()
        {
            Publish(new T());
        }

        /// <summary>
        /// Publishes the specified message and removes all dead subscribers.
        /// </summary>
        /// <typeparam name="T">The type of message to publish.</typeparam>
        /// <param name="message">The message.</param>
        public virtual void Publish<T>(T message)
        {
            WhileLocked(() => Subscribers.RemoveAll(s => !s.Handle(message)));
        }

        /// <summary>
        /// Clears all subscribers.
        /// </summary>
        public virtual void Clear()
        {
            WhileLocked(() => Subscribers.Clear());
        }

        /// <summary>
        /// Creates a subscriber for the specified target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>A subscriber.</returns>
        protected virtual Subscriber CreateSubscriberForTarget(object target)
        {
            return new Subscriber(target);
        }

        private void WhileLocked(Action action)
        {
            lock (_lock) { action(); }
        }

        /// <summary>
        /// A <see cref="Subscriber"/> is a wrapper for an instance subscribed
        /// to messages from a <see cref="MessageBus"/>. It can have many handler methods
        /// which is represented by the <see cref="Handler"/> class.
        /// </summary>
        protected class Subscriber
        {
            protected readonly WeakReference WeakReference;
            protected readonly List<Handler> Handlers;

            /// <summary>
            /// Initializes a new instance of the <see cref="Subscriber" /> class.
            /// </summary>
            /// <param name="target">The target to subscribe.</param>
            public Subscriber(object target)
            {
                Handlers = GetHandlers(target.GetType());
                WeakReference = new WeakReference(target);
            }

            /// <summary>
            /// Checks if the specified target matches the subscribed target.
            /// </summary>
            /// <param name="target">The target to match.</param>
            /// <returns>true if the target matches, false otherwise.</returns>
            public bool Matches(object target)
            {
                return WeakReference.Target == target;
            }

            /// <summary>
            /// Handles the specified message.
            /// </summary>
            /// <typeparam name="T">The type of message to handle.</typeparam>
            /// <param name="message">The message.</param>
            /// <returns>true if the message was handled successfully, false if the target is dead.</returns>
            public bool Handle<T>(T message)
            {
                var target = WeakReference.Target;
                if (target == null) return false;

                Handlers.Where(h => h.CanHandle(typeof(T))).ToList()
                    .ForEach(h => InvokeHandler(message, h, target));

                return true;
            }

            /// <summary>
            /// Handles the specified message.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="message">The message.</param>
            /// <param name="handler">The handler.</param>
            /// <param name="target">The target.</param>
            protected virtual void InvokeHandler<T>(T message, Handler handler, object target)
            {
                if (handler.ShouldMarshalToUIThread)
                {
                    UIThreadMarshaller.Invoke(() => handler.InvokeHandler(target, message));
                    return;
                }

                handler.InvokeHandler(target, message);
            }

            /// <summary>
            /// Gets a list of handlers for the specified type.
            /// </summary>
            /// <param name="targetType">Type of the target.</param>
            /// <returns>List of handlers for the specified type.</returns>
            protected static List<Handler> GetHandlers(Type targetType)
            {
                return targetType.GetHandlerInterfaces()
                    .Select(i => CreateHandler(i, targetType))
                    .ToList();
            }

            /// <summary>
            /// Creates a handler from the given interface type and target type.
            /// </summary>
            /// <param name="interfaceType">Type of the interface.</param>
            /// <param name="targetType">Type of the target.</param>
            /// <returns>A new handler.</returns>
            protected static Handler CreateHandler(Type interfaceType, Type targetType)
            {
                var messageType = interfaceType.FirstGenericArgument();
                var handlerMethod = interfaceType.GetMethod("Handle").ImplementedIn(targetType);

                return new Handler(messageType, handlerMethod);
            }

            /// <summary>
            /// The <see cref="Handler"/> class is a wrapper 
            /// for a method which can handle a specific message type.
            /// </summary>
            protected class Handler
            {
                protected readonly Type MessageType;
                protected readonly MethodInfo Method;

                /// <summary>
                /// Initializes a new instance of the <see cref="Handler" /> class.
                /// </summary>
                /// <param name="messageType">Type of the message.</param>
                /// <param name="method">The method.</param>
                public Handler(Type messageType, MethodInfo method)
                {
                    MessageType = messageType;
                    Method = method;
                    ShouldMarshalToUIThread = method.HasAttribute<HandleOnUIThreadAttribute>();
                }

                /// <summary>
                /// Gets a value indicating whether the message 
                /// handling should be marshalled to the UI thread.
                /// </summary>
                /// <value>
                /// <c>true</c> if the message handling should be marshalled to the UI thread; otherwise, <c>false</c>.
                /// </value>
                public bool ShouldMarshalToUIThread { get; private set; }

                /// <summary>
                /// Determines whether this instance can handle the specified message type.
                /// </summary>
                /// <param name="messageType">Type of the message.</param>
                /// <returns>
                ///   <c>true</c> if this instance can handle the specified message type; otherwise, <c>false</c>.
                /// </returns>
                public bool CanHandle(Type messageType)
                {
                    return MessageType.IsAssignableFrom(messageType);
                }

                /// <summary>
                /// Invokes the handle method on the given target with the given message as argument.
                /// </summary>
                /// <param name="target">The target.</param>
                /// <param name="message">The message.</param>
                public void InvokeHandler(object target, object message)
                {
                    Method.Invoke(target, new[] { message });
                }
            }
        }
    }
}