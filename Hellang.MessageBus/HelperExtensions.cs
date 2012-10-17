using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hellang.MessageBus
{
    internal static class HelperExtensions
    {
        internal static bool HasAttribute<T>(this MemberInfo memberInfo, bool inherit = true)
            where T : Attribute
        {
            return memberInfo.IsDefined(typeof(T), inherit);
        }

        internal static Type FirstGenericArgument(this Type type)
        {
            return type.GetGenericArguments().First();
        }

        internal static IEnumerable<Type> GetMessageTypes(this Type type)
        {
            return type.GetHandleInterfaces().Select(i => i.FirstGenericArgument());
        }

        internal static IEnumerable<Type> GetHandleInterfaces(this Type type)
        {
            return type.GetInterfaces().Where(IsHandleInterface);
        }

        internal static bool IsHandleInterface(this Type type)
        {
            return typeof(IHandle).IsAssignableFrom(type) && type.IsGenericType;
        }

        internal static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                action(item);
            }
        }

        internal static void RemoveAll<T>(this IList<T> list, Func<T, bool> predicate)
        {
            var toRemove = list.Where(predicate).ToList();
            foreach (var item in toRemove)
            {
                list.Remove(item);
            }
        }

        internal static MethodInfo GetHandleMethodFor(this Type type, Type messageType)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .SingleOrDefault(method => method.IsHandleMethodFor(messageType));
        }

        internal static bool IsHandleMethodFor(this MethodInfo method, Type messageType)
        {
            return method.Name == "Handle" 
                && method.ReturnType == typeof(void)
                    && method.HasSingleParameterOfType(messageType);
        }

        internal  static bool HasSingleParameterOfType(this MethodBase method, Type parameterType)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1) return false;

            return parameters.First().ParameterType == parameterType;
        }
    }
}