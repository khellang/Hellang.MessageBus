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

        internal static Type FirstGenericArgument(this Type type)
        {
            return type.GetGenericArguments().First();
        }

        internal static IEnumerable<Type> GetHandlerInterfaces(this Type type)
        {
            return type.GetInterfaces().Where(IsHandleInterface);
        }

        internal static bool IsHandleInterface(Type type)
        {
            return typeof(IHandle).IsAssignableFrom(type) && type.IsGenericType;
        }

        internal static MethodInfo ImplementedIn(this MethodInfo methodInfo, Type targetType)
        {
            if (methodInfo.DeclaringType == null)
                throw new ArgumentException("The method cannot be a global module method.", "methodInfo");

            var map = targetType.GetInterfaceMap(methodInfo.DeclaringType);
            var index = Array.IndexOf(map.InterfaceMethods, methodInfo);

            if (index == -1)
                throw new ArgumentException("Cannot find implementation of method in target type", "targetType");

            return map.TargetMethods[index];
        }
    }
}