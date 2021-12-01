using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hellang.MessageBus
{
    internal static class HelperExtensions
    {
        public static bool HasAttribute<T>(this MemberInfo memberInfo) where T : Attribute =>
            memberInfo.IsDefined(typeof(T), inherit: true);

        public static IEnumerable<Type> GetMessageTypes(this Type type) =>
            type.GetHandleInterfaces().Select(i => i.FirstGenericArgument());

        public static MethodInfo? GetHandleMethodFor(this Type type, Type messageType)
        {
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).SingleOrDefault(m => m.IsHandleMethodFor(messageType));
            if (method is null)
            {
                // Look for explicit method implementation
                var handlerType = typeof(IHandle<>).MakeGenericType(messageType);
                if (handlerType.IsAssignableFrom(type))
                {
                    method = handlerType.GetMethods().SingleOrDefault(m => m.IsHandleMethodFor(messageType));
                }
            }

            return method;
        }

        private static Type FirstGenericArgument(this Type type) =>
            type.GetGenericArguments().First();

        private static IEnumerable<Type> GetHandleInterfaces(this Type type) =>
            type.GetInterfaces().Where(IsHandleInterface);

        private static bool IsHandleInterface(this Type type) =>
            typeof(IHandle).IsAssignableFrom(type) && type.IsGenericType;

        private static bool IsHandleMethodFor(this MethodInfo method, Type messageType)
        {
            return method.Name == nameof(IHandle<object>.Handle)
                && method.ReturnType == typeof(void)
                    && method.HasSingleParameterOfType(messageType);
        }

        private static bool HasSingleParameterOfType(this MethodBase method, Type parameterType)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                return false;
            }

            return parameters.First().ParameterType == parameterType;
        }
    }
}