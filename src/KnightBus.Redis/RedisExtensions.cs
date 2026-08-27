using System;
using KnightBus.Core.Sagas;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KnightBus.Redis;

public static class RedisExtensions
{
    public static IServiceCollection UseRedis(
        this IServiceCollection services,
        Action<IRedisConfiguration>? configuration = null
    )
    {
        var redisConfiguration = new RedisConfiguration();
        configuration?.Invoke(redisConfiguration);
        services.AddSingleton<IRedisConfiguration>(_ => redisConfiguration);
        services.AddSingleton<IConnectionMultiplexer>(provider =>
            ConnectionMultiplexer.Connect(
                provider.GetRequiredService<IRedisConfiguration>().ConnectionString
                    ?? throw new InvalidOperationException("ConnectionString must be configured")
            )
        );
        services.AddScoped<IRedisBus, RedisBus>();
        return services;
    }

    public static IServiceCollection UseRedisSagaStore(this IServiceCollection services)
    {
        services.EnableSagas<RedisSagaStore>();
        return services;
    }
}
