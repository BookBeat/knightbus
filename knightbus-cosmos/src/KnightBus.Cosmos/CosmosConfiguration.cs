using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public interface ICosmosConfiguration : ITransportConfiguration
{
    TimeSpan? PollingDelay { get; set; }
    string Database { get; set; }

    string LeaseContainer { get; set; }

    TimeSpan DefaultTimeToLive { get; set; }

    TimeSpan StartRewind { get; set; }
    CosmosClientOptions ClientOptions { get; set; }
}

public class CosmosConfiguration : ICosmosConfiguration
{
    public CosmosConfiguration()
    {
        if (StartRewind > DefaultTimeToLive)
        {
            throw new ArgumentException("StartRewind should be smaller than TimeToLive");
        }
    }

    public string? ConnectionString { get; set; }
    public IMessageSerializer MessageSerializer { get; set; } = new MicrosoftJsonSerializer(); //TODO pass to CosmosClientOptions
    public TimeSpan? PollingDelay { get; set; } = null;

    public TimeSpan DefaultTimeToLive { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan StartRewind { get; set; } = TimeSpan.Zero;

    public CosmosClientOptions ClientOptions { get; set; } = new() { AllowBulkExecution = true };

    public string Database { get; set; } = "KnightBus";

    public string LeaseContainer { get; set; } = "Leases";
}
