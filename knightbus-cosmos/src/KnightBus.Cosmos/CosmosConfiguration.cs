using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Cosmos;

public interface ICosmosConfiguration : ITransportConfiguration
{
    TimeSpan PollingDelay { get; set; }
    string Database { get; set; }
    
    string Container { get; set; }
    
    TimeSpan DefaultTimeToLive { get; set; }
}

public class CosmosConfiguration : ICosmosConfiguration
{
    public CosmosConfiguration(string connectionString, string database, string container)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
        Database = database;
        Container = container;
    }

    public CosmosConfiguration() { }

    public string ConnectionString { get; set; } = null!;
    public IMessageSerializer MessageSerializer { get; set; } = new MicrosoftJsonSerializer();
    public TimeSpan PollingDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan DefaultTimeToLive { get; set; } = TimeSpan.FromSeconds(60);

    public string Database { get; set; } = null!;
    
    public string Container { get; set; } = null!;
}
