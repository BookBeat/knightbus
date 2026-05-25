using KnightBus.Core.Deduplication;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace KnightBus.Deduplication.Redis;

public static class RedisDeduplicationExtensions
{
    /// <summary>
    /// Enables Redis-backed deduplication with a dedicated Redis connection.
    /// Use this when no <see cref="IDatabase"/> is registered in DI.
    /// </summary>
    public static IServiceCollection UseRedisDeduplication(
        this IServiceCollection services,
        string connectionString,
        Action<RedisDeduplicationOptions>? configure = null
    )
    {
        var options = new RedisDeduplicationOptions { ConnectionString = connectionString };
        configure?.Invoke(options);

        services.AddKeyedSingleton<IDatabase>(
            DeduplicationConstants.RedisDeduplicationKeyedServiceKey,
            (_, _) =>
                ConnectionMultiplexer
                    .Connect(connectionString)
                    .GetDatabase()
                    .WithKeyPrefix(options.KeyPrefix) // prefix redis keys
        );
        services.AddSingleton(options);
        services.EnableDeduplication<RedisDeduplicationStore>();
        return services;
    }
}
