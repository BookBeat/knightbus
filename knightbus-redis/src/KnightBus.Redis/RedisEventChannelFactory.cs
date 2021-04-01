using System;
using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisEventChannelFactory : ITransportChannelFactory
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        public RedisEventChannelFactory(RedisConfiguration configuration, IConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
            Configuration = configuration;
        }

        public ITransportConfiguration Configuration { get; set; }
        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();

        public IChannelReceiver Create(Type messageType, IEventSubscription subscription, IProcessingSettings processingSettings, IMessageSerializer serializer, IHostConfiguration configuration, IMessageProcessor processor)
        {
            var queueReaderType = typeof(RedisEventChannelReceiver<>).MakeGenericType(messageType);
            var queueReader = (IChannelReceiver)Activator.CreateInstance(queueReaderType, _connectionMultiplexer, processingSettings, serializer, subscription, Configuration, configuration, processor);
            return queueReader;
        }

        public bool CanCreate(Type messageType)
        {
            return typeof(IRedisEvent).IsAssignableFrom(messageType);
        }
    }
}