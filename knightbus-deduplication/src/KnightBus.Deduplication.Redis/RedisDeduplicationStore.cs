using KnightBus.Core.Deduplication;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KnightBus.Deduplication.Redis;

public class RedisDeduplicationStore(
    [FromKeyedServices(DeduplicationConstants.RedisDeduplicationKeyedServiceKey)] IDatabase database
) : IDeduplicationStore
{
    public Task<bool> TryClaimAsync(
        string deduplicationKey,
        TimeSpan? ttl,
        CancellationToken cancellationToken
    )
    {
        return database.StringSetAsync(deduplicationKey, "1", ttl, When.NotExists);
    }

    public Task ReleaseAsync(string deduplicationKey, CancellationToken cancellationToken)
    {
        return database.KeyDeleteAsync(deduplicationKey);
    }
}
