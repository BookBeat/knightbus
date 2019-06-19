using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Core.Sagas;
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

        public static IHostConfiguration UseRedisAttachments(this IHostConfiguration configuration, IConnectionMultiplexer multiplexer, RedisConfiguration redisConfiguration)
        {
            configuration.Middlewares.Add(new AttachmentMiddleware(new RedisAttachmentProvider(multiplexer, redisConfiguration)));
            return configuration;
        }

        public static IHostConfiguration UseRedisSagaStore(this IHostConfiguration configuration, IConnectionMultiplexer multiplexer, RedisConfiguration redisConfiguration)
        {
            configuration.EnableSagas(new RedisSagaStore(multiplexer, redisConfiguration.DatabaseId));
            return configuration;
        }
    }
}