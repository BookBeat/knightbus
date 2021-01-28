using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;

namespace KnightBus.Core
{
    internal static class MessageMapper
    {
        private static readonly ConcurrentDictionary<Type, IMessageMapping> Mappings = new ConcurrentDictionary<Type, IMessageMapping>();

        public static void RegisterMappingsFromAssembly(Assembly assembly)
        {
            var type = typeof(IMessageMapping<>);
            var mappings = ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(type, assembly);
            foreach (var mapping in mappings)
            {
                var messageMappingInterface = ReflectionHelper
                    .GetAllInterfacesImplementingOpenGenericInterface(mapping, type).Single();
                var messageType = messageMappingInterface.GenericTypeArguments[0];
                IMessageMapping mappingInstance;
                if (typeof(IMessageMapping).IsAssignableFrom(mapping))
                {
                    mappingInstance = (IMessageMapping) Activator.CreateInstance(mapping);
                }
                else
                {
                    throw new MessageMappingMissingException($"No message mapping exists for {messageType.FullName}");
                }

                RegisterMapping(messageType, mappingInstance);
            }
        }
        public static void RegisterMapping(Type messageType, IMessageMapping mappingType)
        {
            Mappings.GetOrAdd(messageType, t => mappingType);
        }

        public static string GetQueueName(Type t)
        {
            if (Mappings.TryGetValue(t, out var instance))
            {
                return instance.QueueName;
            }

            throw new MessageMappingMissingException($"No queue name mapping exists for {t.FullName}");
        }

        public static IMessageMapping GetMapping(Type t)
        {
            if (Mappings.TryGetValue(t, out var instance))
            {
                return instance;
            }

            throw new MessageMappingMissingException($"No mapping exists for {t.FullName}");
        }
    }
}
