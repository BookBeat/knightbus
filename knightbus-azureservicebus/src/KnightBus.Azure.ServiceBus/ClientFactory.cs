using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Azure.ServiceBus;

[assembly:InternalsVisibleTo("KnightBus.Azure.ServiceBus.Unit")]
[assembly:InternalsVisibleTo("KnightBus.Azure.ServiceBus.Unit")]
namespace KnightBus.Azure.ServiceBus
{
    internal interface IClientFactory
    {
        Task<IQueueClient> GetQueueClient<T>() where T : ICommand;
        Task<ITopicClient> GetTopicClient<T>() where T : IEvent;
        Task<ISubscriptionClient> GetSubscriptionClient<TTopic, TSubscription>(TSubscription subscription) where TTopic : IEvent where TSubscription : IEventSubscription<TTopic>;
    }

    internal class ClientFactory : IClientFactory
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private ConcurrentDictionary<Type, IQueueClient> QueueClients { get; } = new ConcurrentDictionary<Type, IQueueClient>();
        private ConcurrentDictionary<Type, ITopicClient> TopicClients { get; } = new ConcurrentDictionary<Type, ITopicClient>();
        private ConcurrentDictionary<Type, ISubscriptionClient> SubscriptionClients { get; } = new ConcurrentDictionary<Type, ISubscriptionClient>();

        public ClientFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        private IQueueClient CreateQueueClient<T>() where T : ICommand
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            return new QueueClient(_connectionString, queueName, ReceiveMode.PeekLock, RetryPolicy.Default);
        }
        private ITopicClient CreateTopicClient<T>() where T : IEvent
        {
            var topicName = AutoMessageMapper.GetQueueName<T>();
            return new TopicClient(_connectionString, topicName, RetryPolicy.Default);
        }

        private ISubscriptionClient CreateSubscriptionClient<T>(string subscriptionName) where T : IEvent
        {
            var topicName = AutoMessageMapper.GetQueueName<T>();
            return new SubscriptionClient(_connectionString, topicName, subscriptionName, ReceiveMode.PeekLock, RetryPolicy.Default);
        }

        public async Task<IQueueClient> GetQueueClient<T>() where T : ICommand
        {
            if (QueueClients.TryGetValue(typeof(T), out var client))
            {
                return client;
            }

            try
            {
                // No existing client found, try and create one making sure parallel threads do not compete
                await _semaphore.WaitAsync().ConfigureAwait(false);

                // After we have waited, another thread might have created the client we're looking for
                if (QueueClients.TryGetValue(typeof(T), out client))
                {
                    return client;
                }

                client = CreateQueueClient<T>();
                return QueueClients.GetOrAdd(typeof(T), client);
            }
            finally
            {
                _semaphore.Release();
            }
        }


        public async Task<ITopicClient> GetTopicClient<T>() where T : IEvent
        {
            if (TopicClients.TryGetValue(typeof(T), out ITopicClient client))
            {
                return client;
            }

            try
            {
                // No existing client found, try and create one making sure parallel threads do not compete
                await _semaphore.WaitAsync().ConfigureAwait(false);

                // After we have waited, another thread might have created the client we're looking for
                if (TopicClients.TryGetValue(typeof(T), out client))
                {
                    return client;
                }

                client = CreateTopicClient<T>();
                return TopicClients.GetOrAdd(typeof(T), client);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ISubscriptionClient> GetSubscriptionClient<TTopic, TSubscription>(TSubscription subscription) where TTopic : IEvent where TSubscription : IEventSubscription<TTopic>
        {
            if (SubscriptionClients.TryGetValue(typeof(IEventSubscription<TTopic>), out ISubscriptionClient client))
            {
                return client;
            }

            try
            {
                // No existing client found, try and create one making sure parallel threads do not compete
                await _semaphore.WaitAsync().ConfigureAwait(false);

                // After we have waited, another thread might have created the client we're looking for
                if (SubscriptionClients.TryGetValue(typeof(IEventSubscription<TTopic>), out client))
                {
                    return client;
                }

                client = CreateSubscriptionClient<TTopic>(subscription.Name);
                return SubscriptionClients.GetOrAdd(typeof(IEventSubscription<TTopic>), client);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}