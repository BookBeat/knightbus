using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Cosmos;

public interface ICosmosConfiguration : ITransportConfiguration
{
    TimeSpan PollingDelay { get; set; }
}

public class CosmosConfiguration : ICosmosConfiguration
{
    public CosmosConfiguration(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
    }

    public CosmosConfiguration() { }

    public string ConnectionString { get; set; } = null!;
    public IMessageSerializer MessageSerializer { get; set; } = new MicrosoftJsonSerializer();
    public TimeSpan PollingDelay { get; set; } = TimeSpan.FromSeconds(5);

    public string DatabaseId { get; set; } = "sampleDb";
    
    public string ContainerId { get; set; } = "sampleContainer";
}
