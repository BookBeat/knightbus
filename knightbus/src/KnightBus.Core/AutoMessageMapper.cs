using System;
using System.Collections.Concurrent;
using KnightBus.Messages;

namespace KnightBus.Core
{
    public static class AutoMessageMapper
    {
        private static readonly ConcurrentDictionary<string, bool> AlreadyMappedAssemblies = new ConcurrentDictionary<string, bool>();

        private static void MapFromMessageAssembly(Type type)
        {
            var assembly = type.Assembly;
            if (AlreadyMappedAssemblies.ContainsKey(assembly.FullName)) return;

            AlreadyMappedAssemblies.AddOrUpdate(assembly.FullName, false, (s, b) => b);
            MessageMapper.RegisterMappingsFromAssembly(assembly);
        }

        public static string GetQueueName(Type type)
        {
            try
            {
                return MessageMapper.GetQueueName(type);
            }
            catch (MessageMappingMissingException)
            {
                MapFromMessageAssembly(type);
                return MessageMapper.GetQueueName(type);
            }
        }
        public static string GetQueueName<T>() where T : IMessage
        {
            return GetQueueName(typeof(T));
        }
    }
}