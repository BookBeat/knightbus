using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using KnightBus.Core;
using KnightBus.Messages;

[assembly: InternalsVisibleTo("KnightBus.Azure.ServiceBus.Unit")]
namespace KnightBus.Azure.ServiceBus
{
    internal interface IClientFactory : IAsyncDisposable
    {
        Task<ServiceBusSender> GetSenderClient<T>() where T : IMessage;
        Task<ServiceBusProcessor> GetReceiverClient<T>(ServiceBusProcessorOptions options) where T : ICommand;
        Task<ServiceBusProcessor> GetReceiverClient<TTopic, TSubscription>(TSubscription subscription, ServiceBusProcessorOptions options) where TTopic : IEvent where TSubscription : IEventSubscription<TTopic>;
    }

    internal class ClientFactory : IClientFactory
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private ConcurrentDictionary<Type, ServiceBusSender> SenderClients { get; } = new ConcurrentDictionary<Type, ServiceBusSender>();
        private ConcurrentDictionary<Type, ServiceBusProcessor> ReceiverClients { get; } = new ConcurrentDictionary<Type, ServiceBusProcessor>();

        public ClientFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        private ServiceBusSender CreateQueueClient<T>() where T : IMessage
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var client = new ServiceBusClient(_connectionString);
            return client.CreateSender(queueName);
        }

        private ServiceBusProcessor CreateSubscriptionClient<T>(string subscriptionName, ServiceBusProcessorOptions options) where T : IEvent
        {
            var topicName = AutoMessageMapper.GetQueueName<T>();
            var client = new ServiceBusClient(_connectionString);
            return client.CreateProcessor(topicName, subscriptionName, options);
        }

        private ServiceBusProcessor CreateReceiverClient<T>(ServiceBusProcessorOptions options) where T : ICommand
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var client = new ServiceBusClient(_connectionString);
            return client.CreateProcessor(queueName, options);
        }

        public async Task<ServiceBusSender> GetSenderClient<T>() where T : IMessage
        {
            if (SenderClients.TryGetValue(typeof(T), out var client))
            {
                return client;
            }

            try
            {
                // No existing client found, try and create one making sure parallel threads do not compete
                await _semaphore.WaitAsync().ConfigureAwait(false);

                // After we have waited, another thread might have created the client we're looking for
                if (SenderClients.TryGetValue(typeof(T), out client))
                {
                    return client;
                }

                client = CreateQueueClient<T>();
                return SenderClients.GetOrAdd(typeof(T), client);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ServiceBusProcessor> GetReceiverClient<T>(ServiceBusProcessorOptions options) where T : ICommand
        {
            if (ReceiverClients.TryGetValue(typeof(T), out var client))
            {
                return client;
            }

            try
            {
                // No existing client found, try and create one making sure parallel threads do not compete
                await _semaphore.WaitAsync().ConfigureAwait(false);

                // After we have waited, another thread might have created the client we're looking for
                if (ReceiverClients.TryGetValue(typeof(T), out client))
                {
                    return client;
                }

                client = CreateReceiverClient<T>(options);
                return ReceiverClients.GetOrAdd(typeof(T), client);
            }
            finally
            {
                _semaphore.Release();
            }
        }


        public async Task<ServiceBusProcessor> GetReceiverClient<TTopic, TSubscription>(TSubscription subscription, ServiceBusProcessorOptions options) where TTopic : IEvent where TSubscription : IEventSubscription<TTopic>
        {
            if (ReceiverClients.TryGetValue(typeof(IEventSubscription<TTopic>), out var client))
            {
                return client;
            }

            try
            {
                // No existing client found, try and create one making sure parallel threads do not compete
                await _semaphore.WaitAsync().ConfigureAwait(false);

                // After we have waited, another thread might have created the client we're looking for
                if (ReceiverClients.TryGetValue(typeof(IEventSubscription<TTopic>), out client))
                {
                    return client;
                }

                client = CreateSubscriptionClient<TTopic>(subscription.Name, options);
                return ReceiverClients.GetOrAdd(typeof(IEventSubscription<TTopic>), client);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _semaphore?.Dispose();

            foreach (var client in ReceiverClients)
            {
                await client.Value.DisposeAsync();
            }

            ReceiverClients.Clear();

            foreach (var client in SenderClients)
            {
                await client.Value.DisposeAsync();
            }

            SenderClients.Clear();
        }
    }
}