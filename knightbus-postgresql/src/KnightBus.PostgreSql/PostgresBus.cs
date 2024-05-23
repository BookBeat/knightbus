using System.Data;
using System.Text;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
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

    public PostgresBus(NpgsqlDataSource npgsqlDataSource, IPostgresConfiguration postgresConfiguration)
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

        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(null, connection);

        /*
         * Build a cmd text with multirow VALUES syntax and parameter placeholders
         * INSERT INTO queue (visibility_timeout, message) VALUES
         * ('now() + optional delay', ($1)),
         * ('now() + optional delay', ($2)),
         * ('now() + optional delay', ($3));
         */
        var values = new StringBuilder();
        for (int i = 0; i < messagesList.Count; i++)
        {
            var oneBasedIndex = i + 1;
            var trailingComma = oneBasedIndex == messagesList.Count ? string.Empty : ",";
            values.AppendFormat("((now() + interval '{0} seconds'), (${1})){2}",
                delay?.TotalSeconds ?? 0, oneBasedIndex, trailingComma);

            var mBody = _serializer.Serialize(messagesList[i]);
            command.Parameters.Add(new NpgsqlParameter { Value = mBody, NpgsqlDbType = NpgsqlDbType.Jsonb });
        }

        var stringValues = values.ToString();

        command.CommandText = @$"
INSERT INTO {SchemaName}.{QueuePrefix}_{queueName} (visibility_timeout, message)
VALUES {stringValues};
";
        await command.PrepareAsync(ct);
        await command.ExecuteNonQueryAsync(ct);
    }
    private async Task PublishAsyncInternal<T>(IEnumerable<T> messages, CancellationToken ct) where T : IPostgresEvent
    {
        var topicName = AutoMessageMapper.GetQueueName<T>();
        var topicTableName = $"{TopicPrefix}_{topicName}";
        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct);


        await using var cmd = new NpgsqlCommand($"select {SchemaName}.publish_events(@topic_table_name, @messages)", connection);

        var serialized = messages.Select(m => _serializer.Serialize(m)).ToArray();
        
        cmd.CommandType = CommandType.Text;
        cmd.Parameters.Add(new NpgsqlParameter {  ParameterName = "topic_table_name", Value = topicTableName, NpgsqlDbType = NpgsqlDbType.Text});
        cmd.Parameters.Add(new NpgsqlParameter {  ParameterName = "messages", Value = serialized, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Jsonb});
        await cmd.PrepareAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
