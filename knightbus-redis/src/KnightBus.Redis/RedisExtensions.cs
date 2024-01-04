using System;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Core.PreProcessors;
using KnightBus.Core.Sagas;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public static class RedisExtensions
    {
        public static IServiceCollection UseRedisAttachments(this IServiceCollection services)
        {
            services.AddSingleton<IMessageAttachmentProvider, RedisAttachmentProvider>();
            services.AddMiddleware<AttachmentMiddleware>();
            services.AddSingleton<IMessagePreProcessor, AttachmentPreProcessor>();
            return services;
        }

        public static IServiceCollection UseRedis(this IServiceCollection services, Action<IRedisConfiguration> configuration = null)
        {
            var redisConfiguration = new RedisConfiguration();
            configuration?.Invoke(redisConfiguration);
            services.AddSingleton<IRedisConfiguration>(_ => redisConfiguration);
            services.AddSingleton<IConnectionMultiplexer>(provider =>
                ConnectionMultiplexer.Connect(provider.GetRequiredService<IRedisConfiguration>().ConnectionString));
            services.AddScoped<IRedisBus, RedisBus>();
            return services;
        }

        public static IServiceCollection UseRedisSagaStore(this IServiceCollection services)
        {
            services.EnableSagas<RedisSagaStore>();
            return services;
        }
    }
}
