using KnightBus.Core;
using KnightBus.Cosmos.Messages;

namespace KnightBus.Cosmos;

public class CosmosQueueChannelReceiver<T> : IChannelReceiver where T : class, ICosmosEvent
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Not implemented yet");
        throw new NotImplementedException();
    }
    
    public IProcessingSettings Settings { get; set; }
}
