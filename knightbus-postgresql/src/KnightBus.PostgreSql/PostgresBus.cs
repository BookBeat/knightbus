using System.Data;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public interface IPostgresBus
{
    Task SendAsync<T>(T message, CancellationToken ct)
        where T : IPostgresCommand;
    Task PublishAsync<T>(T message, CancellationToken ct)
        where T : IPostgresEvent;
    Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct)
        where T : IPostgresCommand;
    Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct)
        where T : IPostgresEvent;
    Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct)
        where T : IPostgresCommand;
    Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct)
        where T : IPostgresCommand;
}

public class PostgresBus : IPostgresBus
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageSerializer _serializer;
    private readonly IEnumerable<IMessagePreProcessor> _messagePreProcessors;

    public PostgresBus(
        [FromKeyedServices(NpgsqlDataSourceContainerKey)] NpgsqlDataSource npgsqlDataSource,
        IPostgresConfiguration postgresConfiguration,
        IEnumerable<IMessagePreProcessor> messagePreProcessors
    )
    {
        _npgsqlDataSource = npgsqlDataSource;
        _serializer = postgresConfiguration.MessageSerializer;
        _messagePreProcessors = messagePreProcessors;
    }

    public Task SendAsync<T>(T message, CancellationToken ct)
        where T : IPostgresCommand
    {
        return SendAsync([message], ct);
    }

    public Task PublishAsync<T>(T message, CancellationToken ct)
        where T : IPostgresEvent
    {
        return PublishAsyncInternal([message], ct);
    }

    public Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct)
        where T : IPostgresCommand
    {
        return SendAsyncInternal(messages, null, ct);
    }

    public Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct)
        where T : IPostgresEvent
    {
        return PublishAsyncInternal(messages, ct);
    }

    public Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct)
        where T : IPostgresCommand
    {
        return ScheduleAsync([message], delay, ct);
    }

    public Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct)
        where T : IPostgresCommand
    {
        return SendAsyncInternal(messages, delay, ct);
    }

    private async Task SendAsyncInternal<T>(
        IEnumerable<T> messages,
        TimeSpan? delay,
        CancellationToken ct
    )
        where T : IPostgresCommand
    {
        var queueName = AutoMessageMapper.GetQueueName<T>();
        var rows = await SerializeMessagesAsync(messages, ct).ConfigureAwait(false);

        await using var connection = await _npgsqlDataSource
            .OpenConnectionAsync(ct)
            .ConfigureAwait(false);

        if (rows.Count < 50)
        {
            await BatchInsert(connection, queueName, rows, delay, ct).ConfigureAwait(false);
            return;
        }

        await BatchCopy(connection, queueName, rows, delay, ct).ConfigureAwait(false);
    }

    private static async Task BatchInsert(
        NpgsqlConnection connection,
        string queueName,
        List<(byte[] Message, byte[]? Properties)> rows,
        TimeSpan? delay,
        CancellationToken ct
    )
    {
        await using var batch = new NpgsqlBatch(connection);
        foreach (var row in rows)
        {
            batch.BatchCommands.Add(
                new NpgsqlBatchCommand(
                    //lang=postgresql
                    $"INSERT INTO {SchemaName}.{QueuePrefix}_{queueName} (visibility_timeout, message, properties) VALUES (now() + $1, $2, $3)"
                )
                {
                    Parameters =
                    {
                        new NpgsqlParameter<TimeSpan> { TypedValue = delay ?? TimeSpan.Zero },
                        new NpgsqlParameter<byte[]>
                        {
                            TypedValue = row.Message,
                            NpgsqlDbType = NpgsqlDbType.Jsonb,
                        },
                        new NpgsqlParameter
                        {
                            Value = (object?)row.Properties ?? DBNull.Value,
                            NpgsqlDbType = NpgsqlDbType.Jsonb,
                        },
                    },
                }
            );
        }

        await batch.PrepareAsync(ct).ConfigureAwait(false);
        await batch.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task BatchCopy(
        NpgsqlConnection connection,
        string queueName,
        List<(byte[] Message, byte[]? Properties)> rows,
        TimeSpan? delay,
        CancellationToken ct
    )
    {
        string sql =
            //lang=postgresql
            $"COPY {SchemaName}.{QueuePrefix}_{queueName} (visibility_timeout, message, properties) FROM STDIN (FORMAT binary)";

        await using var importer = await connection
            .BeginBinaryImportAsync(sql, ct)
            .ConfigureAwait(false);

        var visibilityTimeout = DateTimeOffset.UtcNow.AddSeconds(delay?.TotalSeconds ?? 0);
        foreach (var row in rows)
        {
            await importer.StartRowAsync(ct).ConfigureAwait(false);
            await importer
                .WriteAsync(visibilityTimeout, NpgsqlDbType.TimestampTz, ct)
                .ConfigureAwait(false);
            await importer.WriteAsync(row.Message, NpgsqlDbType.Jsonb, ct).ConfigureAwait(false);
            if (row.Properties is null)
                await importer.WriteNullAsync(ct).ConfigureAwait(false);
            else
                await importer
                    .WriteAsync(row.Properties, NpgsqlDbType.Jsonb, ct)
                    .ConfigureAwait(false);
        }

        await importer.CompleteAsync(ct).ConfigureAwait(false);
    }

    private async Task PublishAsyncInternal<T>(IEnumerable<T> messages, CancellationToken ct)
        where T : IPostgresEvent
    {
        var topicName = AutoMessageMapper.GetQueueName<T>();
        var rows = await SerializeMessagesAsync(messages, ct).ConfigureAwait(false);
        var serialized = new byte[rows.Count][];
        var properties = new byte[]?[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            serialized[i] = rows[i].Message;
            properties[i] = rows[i].Properties;
        }

        await using var connection = await _npgsqlDataSource
            .OpenConnectionAsync(ct)
            .ConfigureAwait(false);

        try
        {
            await ExecutePublish(connection, topicName, serialized, properties, ct)
                .ConfigureAwait(false);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedFunction)
        {
            // Databases initialized before 4.0.0 lack the properties overload, and consumers
            // only run initialization when a subscription table is missing
            await QueueInitializer.InitPublishFunction(connection).ConfigureAwait(false);
            await ExecutePublish(connection, topicName, serialized, properties, ct)
                .ConfigureAwait(false);
        }
    }

    private static async Task ExecutePublish(
        NpgsqlConnection connection,
        string topicName,
        byte[][] serialized,
        byte[]?[] properties,
        CancellationToken ct
    )
    {
        await using var cmd = new NpgsqlCommand(
            $"select {SchemaName}.publish_events($1, $2, $3)",
            connection
        );

        cmd.CommandType = CommandType.Text;
        cmd.Parameters.Add(
            new NpgsqlParameter { Value = topicName, NpgsqlDbType = NpgsqlDbType.Text }
        );
        cmd.Parameters.Add(
            new NpgsqlParameter
            {
                Value = serialized,
                NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Jsonb,
            }
        );
        cmd.Parameters.Add(
            new NpgsqlParameter
            {
                Value = properties,
                NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Jsonb,
            }
        );
        await cmd.PrepareAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Pre-processors can perform slow I/O such as attachment uploads, so all rows are
    // serialized before a connection is opened or a COPY transaction is started
    private async Task<List<(byte[] Message, byte[]? Properties)>> SerializeMessagesAsync<T>(
        IEnumerable<T> messages,
        CancellationToken ct
    )
        where T : IMessage
    {
        var rows = new List<(byte[], byte[]?)>();
        foreach (var message in messages)
        {
            rows.Add(
                (
                    _serializer.Serialize(message),
                    await SerializePropertiesAsync(message, ct).ConfigureAwait(false)
                )
            );
        }

        return rows;
    }

    private async Task<byte[]?> SerializePropertiesAsync<T>(T message, CancellationToken ct)
        where T : IMessage
    {
        Dictionary<string, string>? properties = null;
        foreach (var preProcessor in _messagePreProcessors)
        {
            var result = await preProcessor.PreProcess(message, ct).ConfigureAwait(false);
            foreach (var property in result)
            {
                properties ??= new Dictionary<string, string>();
                properties[property.Key] = property.Value.ToString() ?? string.Empty;
            }
        }

        return properties is null ? null : _serializer.Serialize(properties);
    }
}
