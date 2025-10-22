using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus;

public interface IClientFactory : IAsyncDisposable
{
    Task<ServiceBusSender> GetSenderClient<T>()
        where T : IMessage;
    Task<ServiceBusProcessor> GetReceiverClient<T>(ServiceBusProcessorOptions options)
        where T : ICommand;
    Task<ServiceBusProcessor> GetReceiverClient<TTopic, TSubscription>(
        TSubscription subscription,
        ServiceBusProcessorOptions options
    )
        where TTopic : IEvent
        where TSubscription : IEventSubscription<TTopic>;
}

public class ClientFactory : IClientFactory
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
    private ConcurrentDictionary<Type, ServiceBusSender> SenderClients { get; } =
        new ConcurrentDictionary<Type, ServiceBusSender>();

    public ClientFactory(string connectionString)
        : this(new ServiceBusConfiguration(connectionString)) { }

    public ClientFactory(IServiceBusConfiguration configuration)
    {
        _serviceBusClient = configuration.CreateServiceBusClient();
    }

    private ServiceBusSender CreateQueueClient<T>()
        where T : IMessage
    {
        var queueName = AutoMessageMapper.GetQueueName<T>();
        return _serviceBusClient.CreateSender(queueName);
    }

    public async Task<ServiceBusSender> GetSenderClient<T>()
        where T : IMessage
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

    public Task<ServiceBusProcessor> GetReceiverClient<T>(ServiceBusProcessorOptions options)
        where T : ICommand
    {
        var queueName = AutoMessageMapper.GetQueueName<T>();
        return Task.FromResult(_serviceBusClient.CreateProcessor(queueName, options));
    }

    public Task<ServiceBusProcessor> GetReceiverClient<TTopic, TSubscription>(
        TSubscription subscription,
        ServiceBusProcessorOptions options
    )
        where TTopic : IEvent
        where TSubscription : IEventSubscription<TTopic>
    {
        var topicName = AutoMessageMapper.GetQueueName<TTopic>();
        return Task.FromResult(
            _serviceBusClient.CreateProcessor(topicName, subscription.Name, options)
        );
    }

    public async ValueTask DisposeAsync()
    {
        _semaphore?.Dispose();

        foreach (var client in SenderClients)
        {
            await client.Value.DisposeAsync();
        }

        SenderClients.Clear();
    }
}
