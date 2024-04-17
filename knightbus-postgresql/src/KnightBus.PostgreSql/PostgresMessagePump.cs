using KnightBus.Core;
using KnightBus.PostgreSql.Messages;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KnightBus.PostgreSql;

public class PostgresMessagePump<T> : GenericMessagePump<PostgresMessage<T>, IPostgresCommand> where T : class, IPostgresCommand
{
    private readonly PostgresQueueClient<T> _queueClient;
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IPostgresConfiguration _postgresConfiguration;

    public PostgresMessagePump(IProcessingSettings settings, PostgresQueueClient<T> queueClient, NpgsqlDataSource npgsqlDataSource, IPostgresConfiguration postgresConfiguration, ILogger log)
        : base(settings, log)
    {
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
        await QueueInitializer.InitQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName(messageType)),
            _npgsqlDataSource);
    }

    protected override bool ShouldCreateChannel(Exception e)
    {
        return e is PostgresException { SqlState: "42P01" };
    }

    protected override async Task CleanupResources()
    {
        await _npgsqlDataSource.DisposeAsync();
    }

    protected override TimeSpan PollingDelay => _postgresConfiguration.PollingDelay;
    protected override int MaxFetch => int.MaxValue;
}
