using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using KnightBus.Messages;

namespace KnightBus.Core
{
    internal static class MessageMapper
    {
        private static readonly ConcurrentDictionary<Type, string> Mappings = new ConcurrentDictionary<Type, string>();

        public static void RegisterMappingsFromAssembly(Assembly assembly)
        {
            var typesToScanFor = new[] { typeof(IMessageMapping<>), typeof(IMessageMapping<>) };
            foreach (var type in typesToScanFor)
            {
                var mappings = ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(type, assembly);
                foreach (var mapping in mappings)
                {
                    var messageMappingInterface = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(mapping, type).Single();
                    var messageType = messageMappingInterface.GenericTypeArguments[0];
                    string value;
                    if (typeof(IMessageMapping).IsAssignableFrom(mapping))
                    {
                        var instance = (IMessageMapping)Activator.CreateInstance(mapping);
                        value = instance.QueueName;
                    }
                    else
                    {
                        throw new MessageMappingMissingException($"No message mapping exists for {messageType.FullName}");
                    }
                    RegisterMapping(messageType, value);
                }
            }
        }
        public static void RegisterMapping(Type mapping, string name)
        {
            Mappings.GetOrAdd(mapping, t => name);
        }

        public static string GetQueueName(Type t)
        {
            if (Mappings.TryGetValue(t, out var queueName))
            {
                return queueName;
            }

            throw new MessageMappingMissingException($"No queue name mapping exists for {t.FullName}");
        }
    }
}