using System.Data;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public interface IPostgresBus
{
    Task SendAsync<T>(T message, CancellationToken ct) where T : IPostgresCommand;
    Task PublishAsync<T>(T message, CancellationToken ct) where T : IPostgresEvent;
    Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : IPostgresCommand;
    Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : IPostgresEvent;
    Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct) where T : IPostgresCommand;
    Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct) where T : IPostgresCommand;
}

public class PostgresBus : IPostgresBus
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageSerializer _serializer;

    public PostgresBus([FromKeyedServices(NpgsqlDataSourceContainerKey)] NpgsqlDataSource npgsqlDataSource, IPostgresConfiguration postgresConfiguration)
    {
        _npgsqlDataSource = npgsqlDataSource;
        _serializer = postgresConfiguration.MessageSerializer;
    }

    public Task SendAsync<T>(T message, CancellationToken ct) where T : IPostgresCommand
    {
        return SendAsync([message], ct);
    }

    public Task PublishAsync<T>(T message, CancellationToken ct) where T : IPostgresEvent
    {
        return PublishAsyncInternal([message], ct);
    }

    public Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : IPostgresCommand
    {
        return SendAsyncInternal(messages, null, ct);
    }

    public Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : IPostgresEvent
    {
        return PublishAsyncInternal(messages, ct);
    }

    public Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct) where T : IPostgresCommand
    {
        return ScheduleAsync([message], delay, ct);
    }

    public Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct) where T : IPostgresCommand
    {
        return SendAsyncInternal(messages, delay, ct);
    }

    private async Task SendAsyncInternal<T>(IEnumerable<T> messages, TimeSpan? delay, CancellationToken ct) where T : IPostgresCommand
    {
        var queueName = AutoMessageMapper.GetQueueName<T>();
        var messagesList = messages.ToList();

        await using var connection = await _npgsqlDataSource
            .OpenConnectionAsync(ct)
            .ConfigureAwait(false);
        await using var batch = new NpgsqlBatch(connection);

        foreach (var message in messagesList)
        {
            var mBody = _serializer.Serialize(message);

            var visibilityTimeout = $"now() + INTERVAL '{delay?.TotalSeconds ?? 0}'";
            var cmd = new NpgsqlBatchCommand(
                $"""
                    INSERT INTO {SchemaName}.{QueuePrefix}_{queueName} (visibility_timeout, message)
                    VALUES ({visibilityTimeout}, $1::JSONB);
                """
            );

            cmd.Parameters.Add(
                new NpgsqlParameter { Value = mBody, NpgsqlDbType = NpgsqlDbType.Jsonb }
            );

            batch.BatchCommands.Add(cmd);
        }

        await batch.PrepareAsync(ct).ConfigureAwait(false);
        await batch.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task PublishAsyncInternal<T>(IEnumerable<T> messages, CancellationToken ct) where T : IPostgresEvent
    {
        var topicName = AutoMessageMapper.GetQueueName<T>();
        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct).ConfigureAwait(false);

        await using var cmd = new NpgsqlCommand($"select {SchemaName}.publish_events($1, $2)", connection);

        var serialized = messages.Select(m => _serializer.Serialize(m)).ToArray();

        cmd.CommandType = CommandType.Text;
        cmd.Parameters.Add(new NpgsqlParameter { Value = topicName, NpgsqlDbType = NpgsqlDbType.Text });
        cmd.Parameters.Add(new NpgsqlParameter { Value = serialized, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Jsonb });
        await cmd.PrepareAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
