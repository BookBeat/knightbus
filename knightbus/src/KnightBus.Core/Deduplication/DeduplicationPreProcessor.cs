using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.PreProcessors;
using KnightBus.Messages;

namespace KnightBus.Core.Deduplication;

public class DeduplicationPreProcessor(IDeduplicationStore store) : IMessagePreProcessor
{
    internal const string DeduplicationKeyProperty = "x-dedup-key";

    public async Task<MessagePreProcessorResult> PreProcess<T>(
        T message,
        CancellationToken cancellationToken
    )
        where T : IMessage
    {
        if (message is not IDeduplicatable deduplicatable)
            return MessagePreProcessorResult.Continue;

        var key = deduplicatable.DeduplicationKey;
        var didClaim = await store.TryClaimAsync(
            key,
            deduplicatable.DeduplicationWindow,
            cancellationToken
        );
        if (!didClaim)
            return MessagePreProcessorResult.Abort();

        return MessagePreProcessorResult.WithProperties(
            new Dictionary<string, object> { { DeduplicationKeyProperty, key } }
        );
    }
}
