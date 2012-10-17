using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hellang.MessageBus
{
    internal static class ReflectionExtensions
    {
        internal static T GetCustomAttribute<T>(this MemberInfo memberInfo, bool inherit = true)
            where T : Attribute
        {
            return memberInfo.GetCustomAttributes(typeof(T), inherit).Cast<T>().FirstOrDefault();
        }

        internal static bool HasAttribute<T>(this MemberInfo memberInfo, bool inherit = true)
            where T : Attribute
        {
            return memberInfo.IsDefined(typeof(T), inherit);
        }

        internal static bool IsAssignableFrom(this Type type, Type otherType)
        {
            return type.GetTypeInfo().IsAssignableFrom(otherType.GetTypeInfo());
        }

        internal static MethodInfo GetMethod(this Type type, string methodName)
        {
            return type.GetTypeInfo().DeclaredMethods.First(m => m.Name == methodName);
        }

        internal static void ForEach<T>(this List<T> list, Action<T> action)
        {
            foreach (var item in list)
            {
                action(item);
            }
        }

        internal static Type FirstGenericArgument(this Type type)
        {
            return type.GenericTypeArguments.First();
        }

        internal static IEnumerable<Type> GetHandlerInterfaces(this Type type)
        {
            return type.GetTypeInfo().ImplementedInterfaces.Where(IsHandleInterface);
        }

        internal static bool IsHandleInterface(Type type)
        {
            var typeInfo = type.GetTypeInfo();
            return typeof(IHandle).GetTypeInfo().IsAssignableFrom(typeInfo) && typeInfo.IsGenericType;
        }

        internal static MethodInfo ImplementedIn(this MethodInfo methodInfo, Type targetType)
        {
            if (methodInfo.DeclaringType == null)
                throw new ArgumentException("The method cannot be a global module method.", "methodInfo");

            var map = targetType.GetTypeInfo().GetRuntimeInterfaceMap(methodInfo.DeclaringType);
            var index = Array.IndexOf(map.InterfaceMethods, methodInfo);

            if (index == -1)
                throw new ArgumentException("Cannot find implementation of method in target type", "targetType");

            return map.TargetMethods[index];
        }
    }
}