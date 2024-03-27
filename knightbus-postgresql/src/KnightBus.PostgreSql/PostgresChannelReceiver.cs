using KnightBus.Core;
using KnightBus.PostgreSql.Messages;

namespace KnightBus.PostgreSql;

public class PostgresChannelReceiver<T> : IChannelReceiver
    where T : class, IPostgresCommand
{
    private readonly IMessageProcessor _processor;
    private readonly IHostConfiguration _hostConfiguration;

    public PostgresChannelReceiver(
        IMessageProcessor processor,
        IProcessingSettings settings,
        IHostConfiguration hostConfiguration)
    {
        _processor = processor;
        Settings = settings;
        _hostConfiguration = hostConfiguration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public IProcessingSettings Settings { get; set; }
}
