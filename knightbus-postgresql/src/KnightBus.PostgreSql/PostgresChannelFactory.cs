using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;

namespace KnightBus.PostgreSql;

public class PostgresChannelFactory : ITransportChannelFactory
{
    public PostgresChannelFactory(ITransportConfiguration configuration)
    {
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
            processingSettings,
            serializer,
            processor,
            configuration,
            Configuration);

        return queueReader;
    }

    public bool CanCreate(Type messageType)
    {
        return typeof(IPostgresCommand).IsAssignableFrom(messageType);
    }
}
