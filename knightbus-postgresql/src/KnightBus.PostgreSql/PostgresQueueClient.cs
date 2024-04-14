using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Npgsql;
using NpgsqlTypes;
using static KnightBus.PostgreSql.Query;

namespace KnightBus.PostgreSql;

public class PostgresQueueClient<T> where T : class, IPostgresCommand
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageSerializer _serializer;
    private readonly PostgresQueueName _queueName;

    public PostgresQueueClient(NpgsqlDataSource npgsqlDataSource, IMessageSerializer serializer)
    {
        _npgsqlDataSource = npgsqlDataSource;
        _serializer = serializer;
        _queueName = PostgresQueueName.Create(AutoMessageMapper.GetQueueName<T>());
    }

    public async Task<PostgresMessage<T>[]> GetMessagesAsync(int count, int visibilityTimeout)
    {
        await using var connection = await _npgsqlDataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(GetMessages(_queueName), connection);

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        command.Parameters.Add(new NpgsqlParameter<TimeSpan> { TypedValue = TimeSpan.FromSeconds(visibilityTimeout) });

        await command.PrepareAsync();

        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<PostgresMessage<T>>();
        while (await reader.ReadAsync())
        {
            var propertiesOrdinal = reader.GetOrdinal("properties");
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<T>
            {
                Id = reader.GetInt64(reader.GetOrdinal("message_id")),
                ReadCount = reader.GetInt32(reader.GetOrdinal("read_count")),
                Message = _serializer
                    .Deserialize<T>(reader.GetFieldValue<byte[]>(
                            reader.GetOrdinal("message"))
                        .AsMemory()),
                Properties = isPropertiesNull
                    ? new Dictionary<string, string>()
                    : _serializer
                        .Deserialize<Dictionary<string, string>>(
                            reader.GetFieldValue<byte[]>(propertiesOrdinal)
                                .AsMemory())
            };
            result.Add(postgresMessage);
        }

        return result.ToArray();
    }

    public async Task CompleteAsync(PostgresMessage<T> message)
    {
        await using var command = _npgsqlDataSource.CreateCommand(CompleteMessage(_queueName));
        command.Parameters.Add(new NpgsqlParameter<long> { Value = message.Id });
        await command.ExecuteNonQueryAsync();
    }

    public async Task AbandonByErrorAsync(PostgresMessage<T> message, Exception exception)
    {
        var errorString = exception.ToString();
        message.Properties["error_message"] = errorString;

        await using var command = _npgsqlDataSource.CreateCommand(AbandonByError(_queueName));
        command.Parameters.Add(new NpgsqlParameter
        {
            Value = _serializer.Serialize(message.Properties), NpgsqlDbType = NpgsqlDbType.Jsonb
        });
        command.Parameters.Add(new NpgsqlParameter<long> { Value = message.Id });
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeadLetterMessageAsync(PostgresMessage<T> message)
    {
        await using var command = _npgsqlDataSource.CreateCommand(DeadLetterMessage(_queueName));
        command.Parameters.Add(new NpgsqlParameter<long> { Value = message.Id });
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<PostgresMessage<T>>> PeekDeadLetterMessagesAsync(int count, CancellationToken ct)
    {
        await using var command = _npgsqlDataSource.CreateCommand(PeekDeadLetterMessage(_queueName));
        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadDeadLetterRows<T>(_serializer, ct);
    }
}
