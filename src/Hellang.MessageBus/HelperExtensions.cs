using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hellang.MessageBus
{
    internal static class HelperExtensions
    {
        /// <summary>
        /// Determines whether the specified member info has an attribute of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of attribute to find.</typeparam>
        /// <param name="memberInfo">The member info.</param>
        /// <returns>
        ///   <c>true</c> if the specified member info has an attribute of the specified type; otherwise, <c>false</c>.
        /// </returns>
        public static bool HasAttribute<T>(this MemberInfo memberInfo)
            where T : Attribute
        {
            return memberInfo.IsDefined(typeof(T), true);
        }

        /// <summary>
        /// Gets the message types from all the handle interfaces of the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>List of message types.</returns>
        public static IEnumerable<Type> GetMessageTypes(this Type type)
        {
            return type.GetHandleInterfaces().Select(i => i.FirstGenericArgument());
        }

        /// <summary>
        /// Applies the specified acion on all items in the specified list.
        /// </summary>
        /// <typeparam name="T">The type of items.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="action">The action.</param>
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                action(item);
            }
        }

        /// <summary>
        /// Removes all items in the specified list which matches the specified predicate.
        /// </summary>
        /// <typeparam name="K">The key type of items.</typeparam>
        /// <typeparam name="V">The value type of items.</typeparam>
        /// <param name="list">The list.</param>
        /// <param name="predicate">The predicate.</param>
        public static int RemoveAll<K, V>(this ConcurrentDictionary<K, V> list, Func<V, bool> predicate)
        {
            int i = 0;
            foreach (var kvp in list.Where(kvp => predicate(kvp.Value)))
            {
                if (list.TryRemove(kvp.Key, out _)) i++;
            }

            return i;
        }

        /// <summary>
        /// Removes all items in the specified list which matches the specified predicate.
        /// </summary>
        /// <typeparam name="K">The key type of items.</typeparam>
        /// <typeparam name="V">The value type of items.</typeparam>
        /// <param name="list">The list.</param>
        /// <param name="predicate">The predicate.</param>
        public static bool RemoveFirst<K, V>(this ConcurrentDictionary<K, V> list, Func<V, bool> predicate)
        {
            foreach (var kvp in list.Where(kvp => predicate(kvp.Value)))
            {
                return (list.TryRemove(kvp.Key, out _));
            }

            return false;
        }

        /// <summary>
        /// Gets the handle method for the specified message type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <returns>The handle method for the specified message type or null.</returns>
        public static MethodInfo GetHandleMethodFor(this Type type, Type messageType)
        {
            var m = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(method => method.IsHandleMethodFor(messageType));
            if (m == null)
            {
                // look for explicit method implementation
                var handlerType = typeof(IHandle<>).MakeGenericType(messageType);
                if (handlerType.IsAssignableFrom(type))
                {
                    m = handlerType.GetMethods().SingleOrDefault(method => method.IsHandleMethodFor(messageType));
                }
            }

            return m;
        }

        /// <summary>
        /// Gets the first generic argument of the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The first generic argument of the specified type.</returns>
        internal static Type FirstGenericArgument(this Type type)
        {
            return type.GetGenericArguments().First();
        }

        /// <summary>
        /// Gets the handle interfaces for the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>List of interface types.</returns>
        internal static IEnumerable<Type> GetHandleInterfaces(this Type type)
        {
            return type.GetInterfaces().Where(IsHandleInterface);
        }

        /// <summary>
        /// Determines whether the specified type is a handle interface.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>
        ///   <c>true</c> if the specified type is a handle interface; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsHandleInterface(this Type type)
        {
            return typeof(IHandle).IsAssignableFrom(type) && type.IsGenericType;
        }

        /// <summary>
        /// Determines whether the specified method is a handler method for the specified message type.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <returns>
        ///   <c>true</c> if the specified method is a handler method for the specified message type; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsHandleMethodFor(this MethodInfo method, Type messageType)
        {
            return method.Name == "Handle"
                   && method.ReturnType == typeof(void)
                   && method.HasSingleParameterOfType(messageType);
        }

        /// <summary>
        /// Determines whether the specified method has a single parameter of the specified type.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="parameterType">Type of the parameter.</param>
        /// <returns>
        ///   <c>true</c> if the specified method has a single parameter of the specified type; otherwise, <c>false</c>.
        /// </returns>
        private static bool HasSingleParameterOfType(this MethodBase method, Type parameterType)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1) return false;

            return parameters.First().ParameterType == parameterType;
        }
    }
}
