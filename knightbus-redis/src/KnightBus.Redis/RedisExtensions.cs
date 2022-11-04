using System;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Core.Sagas;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public static class RedisExtensions
    {
        public static ITransport UseRedisAttachments(this ITransport transport, IConnectionMultiplexer multiplexer, RedisConfiguration configuration)
        {
            transport.UseMiddleware(new AttachmentMiddleware(new RedisAttachmentProvider(multiplexer, configuration)));
            return transport;
        }
        
        public static IServiceCollection UseRedisClient(this IServiceCollection services, Action<IRedisConfiguration> configuration = null)
        {
            var redisConfiguration = new RedisConfiguration();
            configuration?.Invoke(redisConfiguration);
            services.AddSingleton<IRedisBus, RedisBus>();
            services.AddSingleton<IRedisConfiguration>(_ => redisConfiguration);
            services.AddSingleton<IConnectionMultiplexer>(provider =>
                ConnectionMultiplexer.Connect(provider.GetRequiredService<IRedisConfiguration>().ConnectionString));
            return services;
        }
        
        public static IServiceCollection UseRedisAttachments(this IServiceCollection services)
        {
            services.AddSingleton<IMessageAttachmentProvider, RedisAttachmentProvider>();
            return services;
        }

        public static IHostConfiguration UseRedisAttachments(this IHostConfiguration configuration, IConnectionMultiplexer multiplexer, RedisConfiguration redisConfiguration)
        {
            configuration.Middlewares.Add(new AttachmentMiddleware(new RedisAttachmentProvider(multiplexer, redisConfiguration)));
            return configuration;
        }

        public static IHostConfiguration UseRedisSagaStore(this IHostConfiguration configuration, IConnectionMultiplexer multiplexer, RedisConfiguration redisConfiguration)
        {
            configuration.EnableSagas(new RedisSagaStore(multiplexer, redisConfiguration.DatabaseId, redisConfiguration.MessageSerializer));
            return configuration;
        }
    }
}