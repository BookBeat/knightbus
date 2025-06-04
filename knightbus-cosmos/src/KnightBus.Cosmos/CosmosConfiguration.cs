using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Cosmos;

public interface ICosmosConfiguration : ITransportConfiguration
{
    TimeSpan PollingDelay { get; set; }
    string Database { get; set; }

    string LeaseContainer { get; set; }

    TimeSpan DefaultTimeToLive { get; set; }
}

public class CosmosConfiguration : ICosmosConfiguration
{
    public CosmosConfiguration() { }

    public string? ConnectionString { get; set; }
    public IMessageSerializer MessageSerializer { get; set; } = new MicrosoftJsonSerializer(); //TODO remove
    public TimeSpan PollingDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan DefaultTimeToLive { get; set; } = TimeSpan.FromSeconds(60);

    public string Database { get; set; } = "KnightBus";

    public string LeaseContainer { get; set; } = "Leases";
}
