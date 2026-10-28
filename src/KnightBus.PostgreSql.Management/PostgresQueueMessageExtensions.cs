using System.Text;
using KnightBus.Core.Management;
using KnightBus.Messages;

namespace KnightBus.PostgreSql.Management;

internal static class PostgresQueueMessageExtensions
{
    public static async Task<IReadOnlyList<QueueMessage>> ToQueueMessages(
        this IAsyncEnumerable<PostgresMessage<DictionaryMessage>> messages,
        IMessageSerializer serializer
    )
    {
        var result = new List<QueueMessage>();
        await foreach (var m in messages.ConfigureAwait(false))
        {
            m.Properties.TryGetValue("error_message", out var error);
            result.Add(
                new QueueMessage(
                    Encoding.UTF8.GetString(serializer.Serialize(m.Message)),
                    error ?? string.Empty,
                    m.Time,
                    null,
                    m.ReadCount,
                    m.Id.ToString(),
                    m.Properties
                )
            );
        }

        return result;
    }
}
