using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Core.DistributedTracing;
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
        private readonly IMessageAttachmentProvider _attachmentProvider;
        private readonly IDistributedTracingProvider _distributedTracingProvider;

        public StorageBus(IStorageBusConfiguration options, IMessageAttachmentProvider attachmentProvider = null, IDistributedTracingProvider distributedTracingProvider = null)
        {
            _options = options;
            _attachmentProvider = attachmentProvider;
            _distributedTracingProvider = distributedTracingProvider;
        }

        private IStorageQueueClient GetClient<T>() where T : class, ICommand
        {
            return _queueClients.GetOrAdd(typeof(T), type =>
            {
                var serializer = _options.MessageSerializer;
                var mapping = AutoMessageMapper.GetMapping<T>();
                if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;
                return new StorageQueueClient(_options, serializer, _attachmentProvider, _distributedTracingProvider, AutoMessageMapper.GetQueueName<T>());
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
