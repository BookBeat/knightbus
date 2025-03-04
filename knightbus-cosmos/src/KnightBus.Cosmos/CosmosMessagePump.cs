using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace KnightBus.Cosmos;

public class CosmosMessagePump<T> : GenericMessagePump<CosmosMessage<T>, IMessage> where T : class, IMessage
{
    private readonly IEventSubscription? _subscription;
    private readonly CosmosBaseClient<T> _queueClient;
    private readonly ICosmosConfiguration _cosmosConfiguration;

    public CosmosMessagePump(IProcessingSettings settings, IEventSubscription? subscription, CosmosBaseClient<T> queueClient, ICosmosConfiguration cosmosConfiguration, ILogger log)
        : base(settings, log)
    {
        _subscription = subscription;
        _queueClient = queueClient;
        _cosmosConfiguration = cosmosConfiguration;
    }
    
    protected override IAsyncEnumerable<CosmosMessage<T>> GetMessagesAsync<TMessage>(int count, TimeSpan? lockDuration)
    {
        return _queueClient.GetMessagesAsync(
            count,
            lockDuration.HasValue ? (int)lockDuration.Value.TotalSeconds : 0,
            CancellationToken.None);
    }

    protected override async Task CreateChannel(Type messageType)
    {
        if (_subscription is null)
        {
            throw new NotImplementedException("Cosmos queues not implemented");
        }
        else
        {
            throw new NotImplementedException("Cosmos channels not implemented");
        }
    }

    protected override bool ShouldCreateChannel(Exception e)
    {
        throw new NotImplementedException("Cosmos ShouldCreateChannel not implemented");
    }

    protected override async Task CleanupResources()
    {
        throw new NotImplementedException("Cosmos cleanupResources not implemented");
    }

    protected override TimeSpan PollingDelay => _cosmosConfiguration.PollingDelay;
    protected override int MaxFetch => int.MaxValue;
}
