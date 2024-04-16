using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;

namespace KnightBus.PostgreSql;

public interface IPostgresConfiguration : ITransportConfiguration
{
    TimeSpan PollingSleepInterval { get; set; }
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
    public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
    public TimeSpan PollingSleepInterval { get; set; } = TimeSpan.FromSeconds(5);
}
