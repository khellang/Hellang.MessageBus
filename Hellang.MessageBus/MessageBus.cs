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
        private static Action<Action> _uiThreadMarshaller;

        private readonly List<Subscriber> _subscribers = new List<Subscriber>();
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBus" /> class without UI thread marshalling.
        /// </summary>
        public MessageBus() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageBus" /> class.
        /// </summary>
        /// <param name="uiThreadMarshaller">The action for marshalling invocation to the UI thread.</param>
        public MessageBus(Action<Action> uiThreadMarshaller)
        {
            _uiThreadMarshaller = uiThreadMarshaller;
        }

        /// <summary>
        /// Subscribes the specified target to all messages declared
        /// through implementations of <see cref="IHandle{T}" />.
        /// </summary>
        /// <param name="target">The target to subscribe for event publication.</param>
        public void Subscribe(object target)
        {
            WhileLocked(() =>
                {
                    if (!_subscribers.Any(s => s.Matches(target)))
                    {
                        _subscribers.Add(new Subscriber(target));
                    }
                });
        }

        /// <summary>
        /// Unsubscribes the specified target from all events.
        /// </summary>
        /// <param name="target">The target to unsubscribe.</param>
        public void Unsubscribe(object target)
        {
            WhileLocked(() => _subscribers.RemoveAll(s => s.Matches(target)));
        }

        /// <summary>
        /// Publishes a new message of the given message type.
        /// </summary>
        /// <typeparam name="T">The type of message to publish.</typeparam>
        public void Publish<T>() where T : new()
        {
            Publish(new T());
        }

        /// <summary>
        /// Publishes the specified message and removes all dead subscribers.
        /// </summary>
        /// <typeparam name="T">The type of message to publish.</typeparam>
        /// <param name="message">The message.</param>
        public void Publish<T>(T message)
        {
            WhileLocked(() => _subscribers.RemoveAll(s => !s.Handle(message)));
        }

        /// <summary>
        /// Clears all subscribers.
        /// </summary>
        public void Clear()
        {
            WhileLocked(() => _subscribers.Clear());
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
        private class Subscriber
        {
            private static readonly Dictionary<Type, List<Handler>> HandlerCache = new Dictionary<Type,List<Handler>>();

            private readonly WeakReference _weakReference;
            private readonly List<Handler> _handlers;

            /// <summary>
            /// Initializes a new instance of the <see cref="Subscriber" /> class.
            /// </summary>
            /// <param name="target">The target to subscribe.</param>
            public Subscriber(object target)
            {
                _weakReference = new WeakReference(target);
                _handlers = GetHandlers(target);
            }

            /// <summary>
            /// Checks if the specified target matches the subscribed target.
            /// </summary>
            /// <param name="target">The target to match.</param>
            /// <returns>true if the target matches, false otherwise.</returns>
            public bool Matches(object target)
            {
                return _weakReference.Target == target;
            }

            /// <summary>
            /// Handles the specified message.
            /// </summary>
            /// <typeparam name="T">The type of message to handle.</typeparam>
            /// <param name="message">The message.</param>
            /// <returns>true if the message was handled successfully, false if the target is dead.</returns>
            public bool Handle<T>(T message)
            {
                var target = _weakReference.Target;
                if (target == null) return false;

                _handlers.Where(h => h.CanHandle(typeof(T)))
                    .ForEach(h => h.Invoke(target, message));

                return true;
            }

            /// <summary>
            /// Gets the handler methods, either from cache or by reflection.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <returns>List of handlers.</returns>
            private static List<Handler> GetHandlers(object target)
            {
                var targetType = target.GetType();

                List<Handler> handlers;
                if (!HandlerCache.TryGetValue(targetType, out handlers))
                {
                    // No handlers cached, use reflection to get them.
                    handlers = CreateHandlers(targetType).ToList();
                    HandlerCache.Add(targetType, handlers);
                }

                return handlers;
            }

            /// <summary>
            /// Gets a list of handlers for the specified type.
            /// </summary>
            /// <param name="targetType">Type of the target.</param>
            /// <returns>
            /// List of handlers for the specified type.
            /// </returns>
            private static IEnumerable<Handler> CreateHandlers(Type targetType)
            {
                foreach (var messageType in targetType.GetMessageTypes())
                {
                    var handlerMethod = targetType.GetHandleMethodFor(messageType);
                    if (handlerMethod == null) continue;

                    yield return new Handler(messageType, handlerMethod);
                }
            }

            /// <summary>
            /// The <see cref="Handler"/> class is a wrapper 
            /// for a method which can handle a specific message type.
            /// </summary>
            private class Handler
            {
                private readonly Type _messageType;
                private readonly MethodInfo _method;
                private readonly bool _shouldMarshalToUIThread;

                /// <summary>
                /// Initializes a new instance of the <see cref="Handler" /> class.
                /// </summary>
                /// <param name="messageType">Type of the message.</param>
                /// <param name="method">The method.</param>
                public Handler(Type messageType, MethodInfo method)
                {
                    _messageType = messageType;
                    _method = method;
                    _shouldMarshalToUIThread = method.HasAttribute<HandleOnUIThreadAttribute>();
                }

                /// <summary>
                /// Determines whether this instance can handle the specified message type.
                /// </summary>
                /// <param name="messageType">Type of the message.</param>
                /// <returns>
                ///   <c>true</c> if this instance can handle the specified message type; otherwise, <c>false</c>.
                /// </returns>
                public bool CanHandle(Type messageType)
                {
                    return _messageType.IsAssignableFrom(messageType);
                }

                /// <summary>
                /// Invokes the handle method on the given target with the given message as argument.
                /// </summary>
                /// <param name="target">The target.</param>
                /// <param name="message">The message.</param>
                public void Invoke(object target, object message)
                {
                    Action method = () => _method.Invoke(target, new[] { message });

                    if (_shouldMarshalToUIThread && _uiThreadMarshaller != null)
                    {
                        _uiThreadMarshaller.Invoke(method);
                        return;
                    }

                    method();
                }
            }
        }
    }
}