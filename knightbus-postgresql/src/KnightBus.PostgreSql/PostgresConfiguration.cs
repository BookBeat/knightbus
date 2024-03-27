using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;

namespace KnightBus.PostgreSql;

public interface IPostgresConfiguration : ITransportConfiguration { };
public class PostgresConfiguration : IPostgresConfiguration
{
    public PostgresConfiguration(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; set; }
    public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
}
