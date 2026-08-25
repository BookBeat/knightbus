using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Npgsql;

namespace KnightBus.PostgreSql;

public class PostgresChannelFactory : ITransportChannelFactory
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IPostgresConfiguration _postgresConfiguration;

    public PostgresChannelFactory(
        NpgsqlDataSource npgsqlDataSource,
        IPostgresConfiguration postgresConfiguration
    )
    {
        _npgsqlDataSource = npgsqlDataSource;
        _postgresConfiguration = postgresConfiguration;
        Configuration = postgresConfiguration;
    }

    public ITransportConfiguration Configuration { get; set; }

    public IChannelReceiver Create(
        Type messageType,
        IEventSubscription subscription,
        IProcessingSettings processingSettings,
        IMessageSerializer serializer,
        IHostConfiguration configuration,
        IMessageProcessor processor
    )
    {
        var queueReaderType = typeof(PostgresChannelReceiver<>).MakeGenericType(messageType);
        var queueReader =
            Activator.CreateInstance(
                queueReaderType,
                _npgsqlDataSource,
                processor,
                processingSettings,
                configuration,
                serializer,
                _postgresConfiguration
            ) as IChannelReceiver;

        return queueReader
            ?? throw new InvalidOperationException("ChannelReceiver could not be created");
    }

    public bool CanCreate(Type messageType)
    {
        return typeof(IPostgresCommand).IsAssignableFrom(messageType);
    }
}
