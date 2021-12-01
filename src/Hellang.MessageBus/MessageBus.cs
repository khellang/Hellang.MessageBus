using System;
using System.Collections.Concurrent;
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
    // ReSharper disable once InconsistentNaming
    public class HandleOnUIThreadAttribute : Attribute { }

    /// <summary>
    /// Enables loosely-coupled publication of and subscription to messages.
    /// </summary>
    public class MessageBus : IMessageBus
    {
        private readonly List<Subscriber> _subscribers = new();

        private readonly object _lock = new();

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
            UiThreadMarshaller = uiThreadMarshaller ?? throw new ArgumentNullException(nameof(uiThreadMarshaller));
        }

        private Action<Action>? UiThreadMarshaller { get; }

        /// <summary>
        /// Subscribes the specified target to all messages declared
        /// through implementations of <see cref="IHandle{T}" />.
        /// </summary>
        /// <param name="target">The target to subscribe for event publication.</param>
        public void Subscribe(object target)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            lock (_lock)
            {
                foreach (var subscriber in _subscribers)
                {
                    if (subscriber.Matches(target))
                    {
                        return;
                    }
                }

                _subscribers.Add(new Subscriber(target));
            }
        }

        /// <summary>
        /// Unsubscribes the specified target from all events.
        /// </summary>
        /// <param name="target">The target to unsubscribe.</param>
        public void Unsubscribe(object target)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            lock (_lock)
            {
                _subscribers.RemoveAll(s => s.Matches(target));
            }
        }

        /// <summary>
        /// Publishes a new message of the given message type.
        /// </summary>
        /// <typeparam name="T">The type of message to publish.</typeparam>
        public void Publish<T>() where T : notnull, new() => Publish(new T());

        /// <summary>
        /// Publishes the specified message and removes all dead subscribers.
        /// </summary>
        /// <typeparam name="T">The type of message to publish.</typeparam>
        /// <param name="message">The message.</param>
        public void Publish<T>(T message) where T : notnull
        {
            lock (_lock)
            {
                _subscribers.RemoveAll(s => !s.Handle(message, UiThreadMarshaller));
            }
        }

        /// <summary>
        /// Clears all subscribers.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _subscribers.Clear();
            }
        }

        private class Subscriber
        {
            private static readonly ConcurrentDictionary<Type, IList<Handler>> HandlerCache = new();

            public Subscriber(object target)
            {
                WeakReference = new WeakReference(target);
                Handlers = GetHandlers(target.GetType());
            }

            private WeakReference WeakReference { get; }

            private IList<Handler> Handlers { get; }

            public bool Matches(object target) => WeakReference.Target == target;

            public bool Handle<T>(T message, Action<Action>? uiThreadMarshaller) where T : notnull
            {
                var target = WeakReference.Target;
                if (target is null)
                {
                    return false;
                }

                foreach (var handler in Handlers.Where(h => h.CanHandle(typeof(T))))
                {
                    handler.Handle(target, message, uiThreadMarshaller);
                }
                return true;
            }

            private static IList<Handler> GetHandlers(Type targetType) =>
                HandlerCache.GetOrAdd(targetType, t => CreateHandlers(t).ToArray());

            private static IEnumerable<Handler> CreateHandlers(Type targetType)
            {
                foreach (var messageType in targetType.GetMessageTypes())
                {
                    var handlerMethod = targetType.GetHandleMethodFor(messageType);
                    if (handlerMethod is null)
                    {
                        continue;
                    }

                    yield return new Handler(messageType, handlerMethod);
                }
            }

            private class Handler
            {
                public Handler(Type messageType, MethodInfo method)
                {
                    MessageType = messageType;
                    Method = method;
                    ShouldMarshalToUiThread = method.HasAttribute<HandleOnUIThreadAttribute>();
                }

                private Type MessageType { get; }

                private MethodInfo Method { get; }

                private bool ShouldMarshalToUiThread { get; }

                public bool CanHandle(Type messageType) =>
                    MessageType.IsAssignableFrom(messageType);

                public void Handle(object target, object message, Action<Action>? uiThreadMarshaller)
                {
                    void Handler() => Method.Invoke(target, new[] { message });

                    if (ShouldMarshalToUiThread)
                    {
                        if (uiThreadMarshaller is null)
                        {
                            throw new NotSupportedException("Marshalling calls to the UI thread is not supported. " +
                                "Use the Action<Action> overload of the constructor to specify a UI thread mashalling function.");
                        }

                        uiThreadMarshaller.Invoke(Handler);
                    }
                    else
                    {
                        Handler();
                    }
                }
            }
        }
    }
}