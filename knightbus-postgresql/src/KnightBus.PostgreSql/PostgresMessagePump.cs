using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KnightBus.PostgreSql;

public class PostgresMessagePump<T> : GenericMessagePump<PostgresMessage<T>, IMessage> where T : class, IMessage
{
    private readonly IEventSubscription? _subscription;
    private readonly PostgresBaseClient<T> _queueClient;
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IPostgresConfiguration _postgresConfiguration;

    public PostgresMessagePump(IProcessingSettings settings, IEventSubscription? subscription, PostgresBaseClient<T> queueClient, NpgsqlDataSource npgsqlDataSource, IPostgresConfiguration postgresConfiguration, ILogger log)
        : base(settings, log)
    {
        _subscription = subscription;
        _queueClient = queueClient;
        _npgsqlDataSource = npgsqlDataSource;
        _postgresConfiguration = postgresConfiguration;
    }
    
    protected override IAsyncEnumerable<PostgresMessage<T>> GetMessagesAsync<TMessage>(int count, TimeSpan? lockDuration)
    {
        return _queueClient.GetMessagesAsync(
            count,
            lockDuration.HasValue ? (int)lockDuration.Value.TotalSeconds : 0,
            CancellationToken.None);
    }

    protected override async Task CreateChannel(Type messageType)
    {
        if (_subscription is null)
        {
            await QueueInitializer.InitQueue(
                PostgresQueueName.Create(AutoMessageMapper.GetQueueName(messageType)),
                _npgsqlDataSource);    
        }
        else
        {
            await QueueInitializer.InitSubscription(
                PostgresQueueName.Create(AutoMessageMapper.GetQueueName(messageType)),
                PostgresQueueName.Create(_subscription.Name),
                _npgsqlDataSource);
        }
    }

    protected override bool ShouldCreateChannel(Exception e)
    {
        return e is PostgresException { SqlState: PostgresErrorCodes.UndefinedTable };
    }

    protected override async Task CleanupResources()
    {
        await _npgsqlDataSource.DisposeAsync();
    }

    protected override TimeSpan PollingDelay => _postgresConfiguration.PollingDelay;
    protected override int MaxFetch => int.MaxValue;
}
