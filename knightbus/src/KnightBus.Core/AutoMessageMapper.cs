using System;
using System.Collections.Concurrent;
using System.Threading;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;

namespace KnightBus.Core
{
    public static class AutoMessageMapper
    {
        private static readonly ConcurrentDictionary<string, bool> AlreadyMappedAssemblies = new ConcurrentDictionary<string, bool>();
        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1,1);

        private static void MapFromMessageAssembly(Type type)
        {
            var assembly = type.Assembly;
            if (AlreadyMappedAssemblies.ContainsKey(assembly.FullName)) return;

            // This is so that we do not call RegisterMappingsFromAssembly twice for the same assembly.
            // While the assembly is being scanned, it won't be in AlreadyMappedAssemblies, and
            // we can't add it to AlreadyMappedAssemblies before it has finished scanning because
            // that will give us a race condition and throw MessageMappingMissingException.
            try
            {
                Semaphore.Wait();

                // If the assembly was mapped while we were waiting for the semaphore to release, return
                if (AlreadyMappedAssemblies.ContainsKey(assembly.FullName)) return;

                MessageMapper.RegisterMappingsFromAssembly(assembly);
                AlreadyMappedAssemblies.AddOrUpdate(assembly.FullName, false, (s, b) => b);
            }
            finally
            {
                Semaphore.Release();
            }
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
