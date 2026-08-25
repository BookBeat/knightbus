using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.PostgreSql;

public interface IPostgresConfiguration : ITransportConfiguration
{
    TimeSpan PollingDelay { get; set; }
}

public class PostgresConfiguration : IPostgresConfiguration
{
    public PostgresConfiguration(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
    }

    public PostgresConfiguration() { }

    public string ConnectionString { get; set; } = null!;
    public IMessageSerializer MessageSerializer { get; set; } = new MicrosoftJsonSerializer();
    public TimeSpan PollingDelay { get; set; } = TimeSpan.FromSeconds(5);
}
