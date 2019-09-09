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
            return assemblies.SelectMany(assembly => GetAllTypesImplementingOpenGenericInterface(openGenericType, assembly));
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

        public static IEnumerable<Type> GetAllTypesImplementingInterface(Type targetInterface, Assembly[] assemblies)
        {
            return assemblies.SelectMany(assembly => GetAllTypesImplementingInterface(targetInterface, assembly));
        }

        public static IEnumerable<Type> GetAllTypesImplementingInterface(Type targetInterface, Assembly assembly)
        {
            try
            {
                return GetAllTypesImplementingInterface(targetInterface, assembly.GetTypes());
            }
            catch (ReflectionTypeLoadException)
            {
                //It's expected to not being able to load all assemblies
                return new List<Type>();
            }
        }
        public static IEnumerable<Type> GetAllTypesImplementingInterface(Type targetInterface, IEnumerable<Type> types)
        {

            return from type in types
                from interfaceType in type.GetInterfaces()
                where
                    (targetInterface.IsAssignableFrom(interfaceType) &&
                     type.IsClass && !type.IsAbstract)
                select type;
        }
    }
}