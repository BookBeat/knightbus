using KnightBus.Messages;
using Npgsql;

namespace KnightBus.PostgreSql;

public static class NpgsqlDataReaderExtensions
{
    public static async Task<List<PostgresMessage<T>>> ReadDeadLetterRows<T>(
        this NpgsqlDataReader reader, IMessageSerializer serializer, CancellationToken ct)
        where T : class, IMessage
    {
        var result = new List<PostgresMessage<T>>();
        while (await reader.ReadAsync(ct))
        {
            var propertiesOrdinal = reader.GetOrdinal("properties");
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<T>
            {
                Id = reader.GetInt64(reader.GetOrdinal("message_id")),
                Message = serializer
                    .Deserialize<T>(reader.GetFieldValue<byte[]>(
                            reader.GetOrdinal("message"))
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
