using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public interface ICosmosConfiguration : ITransportConfiguration
{
    TimeSpan PollingDelay { get; set; }
    string Database { get; set; }

    string LeaseContainer { get; set; }

    TimeSpan DefaultTimeToLive { get; set; }

    TimeSpan StartRewind { get; set; }
    CosmosClientOptions ClientOptions { get; set; }

    int MaxPublishRetriesOnRateLimited { get; set; }

    int MaxRUs { get; set; }
}

public class CosmosConfiguration : ICosmosConfiguration
{
    public CosmosConfiguration() { }

    public string? ConnectionString { get; set; }
    public IMessageSerializer MessageSerializer { get; set; } = new MicrosoftJsonSerializer(); //TODO remove?
    public TimeSpan PollingDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan DefaultTimeToLive { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan StartRewind { get; set; } = TimeSpan.Zero;

    public CosmosClientOptions ClientOptions { get; set; } = new() { AllowBulkExecution = true };

    public string Database { get; set; } = "KnightBus";

    public string LeaseContainer { get; set; } = "Leases";

    public int MaxPublishRetriesOnRateLimited { get; set; } = 5;

    public int MaxRUs { get; set; } = 1000;
}
