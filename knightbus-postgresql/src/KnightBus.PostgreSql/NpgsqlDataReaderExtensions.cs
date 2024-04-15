using KnightBus.Messages;
using Npgsql;

namespace KnightBus.PostgreSql;

public static class NpgsqlDataReaderExtensions
{
    public static async Task<List<PostgresMessage<T>>> ReadDeadLetterRows<T>(
        this NpgsqlDataReader reader, IMessageSerializer serializer, CancellationToken ct)
        where T : class, IMessage
    {
        var propertiesOrdinal = reader.GetOrdinal("properties");
        var messageIdOrdinal = reader.GetOrdinal("message_id");
        var messageOrdinal = reader.GetOrdinal("message");

        var result = new List<PostgresMessage<T>>();
        while (await reader.ReadAsync(ct))
        {
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<T>
            {
                Id = reader.GetInt64(messageIdOrdinal),
                Message = serializer
                    .Deserialize<T>(reader.GetFieldValue<byte[]>(messageOrdinal)
                        .AsMemory()),
                Properties = isPropertiesNull
                    ? new Dictionary<string, string>()
                    : serializer
                        .Deserialize<Dictionary<string, string>>(
                            reader.GetFieldValue<byte[]>(propertiesOrdinal)
                                .AsMemory())
            };
            result.Add(postgresMessage);
        }

        return result;
    }

    public static async Task<List<PostgresMessage<T>>> ReadMessageRows<T>(
        this NpgsqlDataReader reader, IMessageSerializer serializer, CancellationToken ct = default)
        where T : class, IMessage
    {
        var propertiesOrdinal = reader.GetOrdinal("properties");
        var messageIdOrdinal = reader.GetOrdinal("message_id");
        var readCountOrdinal = reader.GetOrdinal("read_count");
        var messageOrdinal = reader.GetOrdinal("message");

        var result = new List<PostgresMessage<T>>();
        while (await reader.ReadAsync(ct))
        {
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<T>
            {
                Id = reader.GetInt64(messageIdOrdinal),
                ReadCount = reader.GetInt32(readCountOrdinal),
                Message = serializer
                    .Deserialize<T>(reader.GetFieldValue<byte[]>(messageOrdinal)
                        .AsMemory()),
                Properties = isPropertiesNull
                    ? new Dictionary<string, string>()
                    : serializer
                        .Deserialize<Dictionary<string, string>>(
                            reader.GetFieldValue<byte[]>(propertiesOrdinal)
                                .AsMemory())
            };
            result.Add(postgresMessage);
        }

        return result;
    }
}
