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
    public interface IHandle
    {
    }

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
    public class HandleOnUiThreadAttribute : Attribute
    {
    }

    /// <summary>
    /// Enables loosely-coupled publication of and subscription to messages.
    /// </summary>
    public class MessageBus : IMessageBus
    {
        private static Action<Action> uiThreadMarshaller;

        private readonly ConcurrentDictionary<int, Subscriber>
            subscribers = new ConcurrentDictionary<int, Subscriber>();

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
            MessageBus.uiThreadMarshaller = uiThreadMarshaller;
        }

        /// <summary>
        /// Subscribes the specified target to all messages declared
        /// through implementations of <see cref="IHandle{T}" />.
        /// </summary>
        /// <param name="target">The target to subscribe for event publication.</param>
        public bool Subscribe(object target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var hash = target.GetHashCode();
            return subscribers.TryAdd(hash, new Subscriber(target, hash));
        }

        /// <summary>
        /// Unsubscribes the specified target from all events.
        /// </summary>
        /// <param name="target">The target to unsubscribe.</param>
        public bool Unsubscribe(object target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var hash = target.GetHashCode();
            return subscribers.TryRemove(hash, out _);
        }

        /// <summary>
        /// Publishes a new message of the given message type.
        /// </summary>
        /// <typeparam name="T">The type of message to publish.</typeparam>
        public void Publish<T>() where T : new()
        {
            PublishCore(new T(), typeof(T));
        }

        /// <summary>
        /// Publishes the specified message and removes all dead subscribers.
        /// </summary>
        /// <typeparam name="T">The type of message to publish.</typeparam>
        /// <param name="message">The message.</param>
        public void Publish<T>(T message)
        {
            PublishCore(message, message.GetType());
        }

        /// <summary>
        /// Publishes the specified message and removes all dead subscribers.
        /// </summary>
        protected virtual void PublishCore<T>(T message, Type t)
        {
            subscribers.RemoveAll(s => !s.Handle(message, t));
        }

        /// <summary>
        /// Clears all subscribers.
        /// </summary>
        public void Clear()
        {
            subscribers.Clear();
        }


        /// <summary>
        /// A <see cref="Subscriber"/> is a wrapper for an instance subscribed
        /// to messages from a <see cref="MessageBus"/>. It can have many handler methods
        /// which is represented by the <see cref="Handler&lt;T&gt;"/> class.
        /// </summary>
        private class Subscriber : IEquatable<Subscriber>
        {
            private static readonly ConcurrentDictionary<Type, IList<IHandler>> handlerCache =
                new ConcurrentDictionary<Type, IList<IHandler>>();

            private readonly WeakReference weakReference;
            private readonly IList<IHandler> handlers;
            private readonly int hash;

            /// <summary>
            /// Initializes a new instance of the <see cref="Subscriber" /> class.
            /// </summary>
            /// <param name="target">The target to subscribe.</param>
            /// <param name="hash"></param>
            public Subscriber(object target, int hash)
            {
                if (target == null) throw new ArgumentNullException(nameof(target));
                this.hash = hash;
                weakReference = new WeakReference(target);
                handlers = GetHandlers(target);
            }

            //internal bool IsAlive => _weakReference.IsAlive;

            /// <summary>
            /// Handles the specified message.
            /// </summary>
            /// <typeparam name="T">The type of message to handle.</typeparam>
            /// <param name="message">The message.</param>
            /// <param name="messageType"></param>
            /// <returns>true if the target handler is alive, false if the target is dead.</returns>
            public bool Handle<T>(T message, Type messageType)
            {
                var target = weakReference.Target;
                if (target == null) return false;
                // high traffic method, do not use linq here.
                foreach (var h in handlers)
                {
                    if (!h.CanHandle(messageType)) continue;
                    h.Invoke(target, message);
                }

                return true;
            }

            /// <summary>
            /// Gets the handler methods, either from cache or by reflection.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <returns>List of handlers.</returns>
            private static IList<IHandler> GetHandlers(object target)
            {
                var targetType = target.GetType();
                var handlers = handlerCache.GetOrAdd(targetType, AddFactory);
                return handlers;

                static IList<IHandler> AddFactory(Type targetType)
                {
                    // No handlers cached, use reflection to get them.
                    return CreateHandlers(targetType).ToArray();
                }
            }

            /// <summary>
            /// Gets a list of handlers for the specified type.
            /// </summary>
            /// <param name="targetType">Type of the target.</param>
            /// <returns>
            /// List of handlers for the specified type.
            /// </returns>
            private static IEnumerable<IHandler> CreateHandlers(Type targetType)
            {
                foreach (var handleInterface in targetType.GetHandleInterfaces())
                {
                    var messageType = handleInterface.FirstGenericArgument();
                    var handlerMethod = targetType.GetHandleMethodFor(messageType);
                    if (handlerMethod == null) continue;
                    var handler = (IHandler)Activator.CreateInstance(typeof(Handler<>).MakeGenericType(messageType));
                    handler.Initialize(handlerMethod.HasAttribute<HandleOnUiThreadAttribute>());
                    yield return handler;
                }
            }

            /// <summary>
            /// Checks if the specified target matches the subscribed target.
            /// </summary>
            /// <param name="target">The target to match.</param>
            /// <returns>true if the target matches, false otherwise.</returns>
            public bool Matches(object target)
            {
                return weakReference.IsAlive && Equals(weakReference.Target, target);
            }

            public bool Equals(Subscriber other)
            {
                if (other is null || !weakReference.IsAlive || !other.weakReference.IsAlive) return false;
                if (ReferenceEquals(this, other)) return true;
                return hash == other.hash || Equals(weakReference.Target, other.weakReference.Target);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Subscriber)obj);
            }

            public override int GetHashCode() => hash;

            public static bool operator ==(Subscriber left, Subscriber right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(Subscriber left, Subscriber right)
            {
                return !Equals(left, right);
            }

            /// <summary>
            /// The <see cref="Handler&lt;T&gt;"/> class is a wrapper 
            /// for a method which can handle a specific message type.
            /// </summary>
            private class Handler<T> : IHandler
            {
                private readonly Type messageType;

                //private readonly MethodInfo method;
                private bool shouldMarshalToUiThread;

                /// <summary>
                /// Initializes a new instance of the <see cref="Handler&lt;T&gt;" /> class.
                /// </summary> 
                public Handler()
                {
                    this.messageType = typeof(T);
                }

                /// <summary>
                /// Initializes a new instance of the <see cref="Handler&lt;T&gt;" /> class.
                /// </summary>
                /// <param name="shouldMarshalToUi">true if invocation should be performed on UI thread</param>
                public void Initialize(bool shouldMarshalToUi)
                {
                    this.shouldMarshalToUiThread = shouldMarshalToUi;
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
                    return this.messageType.IsAssignableFrom(messageType);
                }

                /// <summary>
                /// Invokes the handle method on the given target with the given message as argument.
                /// </summary>
                /// <param name="target">The target.</param>
                /// <param name="message">The message.</param>
                public void Invoke(object target, object message)
                {
                    void Method()
                    {
                        var actualHandler = (IHandle<T>)target;
                        actualHandler.Handle((T)message);
                    }

                    if (shouldMarshalToUiThread && uiThreadMarshaller != null)
                    {
                        uiThreadMarshaller.Invoke(Method);
                        return;
                    }

                    Method();
                }
            }
        }
    }

    internal interface IHandler
    {
        void Initialize(bool shouldMarshalToUiThread);
        bool CanHandle(Type messageType);
        void Invoke(object target, object message);
    }
}
