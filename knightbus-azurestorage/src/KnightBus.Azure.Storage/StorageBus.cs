using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage
{
    public interface IStorageBus
    {
        Task SendAsync<T>(T command, CancellationToken cancellationToken = default) where T : class, IStorageQueueCommand;
        Task ScheduleAsync<T>(T command, TimeSpan delay, CancellationToken cancellationToken = default) where T : class, IStorageQueueCommand;
    }

    public class StorageBus : IStorageBus
    {
        private readonly IStorageBusConfiguration _options;
        private readonly ConcurrentDictionary<Type, IStorageQueueClient> _queueClients = new ConcurrentDictionary<Type, IStorageQueueClient>();
        private readonly IEnumerable<IMessagePreProcessor> _messagePreProcessors;

        public StorageBus(IStorageBusConfiguration options, IEnumerable<IMessagePreProcessor> messagePreProcessors)
        {
            _options = options;
            _messagePreProcessors = messagePreProcessors;
        }

        private IStorageQueueClient GetClient<T>() where T : class, ICommand
        {
            return _queueClients.GetOrAdd(typeof(T), type =>
            {
                var serializer = _options.MessageSerializer;
                var mapping = AutoMessageMapper.GetMapping<T>();
                if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;
                return new StorageQueueClient(_options, serializer, _messagePreProcessors, AutoMessageMapper.GetQueueName<T>());
            });
        }

        private Task SendAsync<T>(T command, TimeSpan? delay, CancellationToken cancellationToken = default) where T : class, IStorageQueueCommand
        {
            return GetClient<T>().SendAsync(command, delay, cancellationToken);
        }

        public Task SendAsync<T>(T command, CancellationToken cancellationToken = default) where T : class, IStorageQueueCommand
        {
            return SendAsync(command, null, cancellationToken);
        }

        public Task ScheduleAsync<T>(T command, TimeSpan delay, CancellationToken cancellationToken = default) where T : class, IStorageQueueCommand
        {
            return SendAsync(command, delay, cancellationToken);
        }
    }
}
