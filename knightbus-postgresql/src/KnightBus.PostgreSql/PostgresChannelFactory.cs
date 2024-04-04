using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Npgsql;

namespace KnightBus.PostgreSql;

public class PostgresChannelFactory : ITransportChannelFactory
{
    private readonly NpgsqlDataSource _npgsqlDataSource;

    public PostgresChannelFactory(ITransportConfiguration configuration, NpgsqlDataSource npgsqlDataSource)
    {
        _npgsqlDataSource = npgsqlDataSource;
        Configuration = configuration;
    }

    public ITransportConfiguration Configuration { get; set; }

    public IChannelReceiver Create(
        Type messageType,
        IEventSubscription subscription,
        IProcessingSettings processingSettings,
        IMessageSerializer serializer,
        IHostConfiguration configuration,
        IMessageProcessor processor)
    {
        var queueReaderType = typeof(PostgresChannelReceiver<>).MakeGenericType(messageType);
        var queueReader = (IChannelReceiver)Activator.CreateInstance(
            queueReaderType,
            _npgsqlDataSource,
            processor,
            processingSettings,
            configuration,
            serializer);

        return queueReader;
    }

    public bool CanCreate(Type messageType)
    {
        return typeof(IPostgresCommand).IsAssignableFrom(messageType);
    }
}
