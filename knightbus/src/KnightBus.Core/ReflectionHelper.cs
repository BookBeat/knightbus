using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KnightBus.Core
{
    public static class ReflectionHelper
    {
        public static IEnumerable<Type> GetAllInterfacesImplementingOpenGenericInterface(Type type, Type openGenericType)
        {
            var interfaces = type.GetInterfaces();
            return interfaces.Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == openGenericType);
        }
        public static IEnumerable<Type> GetAllTypesImplementingOpenGenericInterface(Type openGenericType, Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                foreach (var type in GetAllTypesImplementingOpenGenericInterface(openGenericType, assembly))
                {
                    yield return type;
                }
            }
        }

        public static IEnumerable<Type> GetAllTypesImplementingOpenGenericInterface(Type openGenericType, Assembly assembly)
        {
            try
            {
                return GetAllTypesImplementingOpenGenericInterface(openGenericType, assembly.GetTypes());
            }
            catch (ReflectionTypeLoadException)
            {
                //It's expected to not being able to load all assemblies
                return new List<Type>();
            }
        }
        public static IEnumerable<Type> GetAllTypesImplementingOpenGenericInterface(Type openGenericType, IEnumerable<Type> types)
        {

            return from type in types
                   from interfaceType in type.GetInterfaces()
                   where
                       (interfaceType.IsGenericType &&
                        openGenericType.IsAssignableFrom(interfaceType.GetGenericTypeDefinition()) &&
                        type.IsClass && !type.IsAbstract)
                   select type;

        }
    }
}