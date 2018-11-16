using System;
using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Redis.Messages;

namespace KnightBus.Redis
{
    public class RedisChannelFactory : ITransportChannelFactory
    {
        public RedisChannelFactory(RedisConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ITransportConfiguration Configuration { get; set; }
        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();
        public IChannelReceiver Create(Type messageType, Type subscriptionType, Type settingsType, IHostConfiguration configuration, IMessageProcessor processor)
        {
            var settings = Activator.CreateInstance(settingsType);
            var queueReaderType = typeof(RedisChannelReceiver<>).MakeGenericType(messageType);
            var queueReader = (IChannelReceiver)Activator.CreateInstance(queueReaderType, settings, Configuration, configuration, processor);
            return queueReader;
        }

        public bool CanCreate(Type messageType)
        {
            return typeof(IRedisCommand).IsAssignableFrom(messageType);
        }
    }
}