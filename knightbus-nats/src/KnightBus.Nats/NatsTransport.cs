using System;
using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using NATS.Client;

namespace KnightBus.Nats
{
    public class NatsTransport : ITransport
    {
        public NatsTransport(string connectionString)
            : this(new NatsBusConfiguration(connectionString))
        {
            
        }
        public NatsTransport(INatsBusConfiguration configuration)
        {
            TransportChannelFactories = new ITransportChannelFactory[] {new NatsChannelFactory(configuration),};
        }
        public ITransportChannelFactory[] TransportChannelFactories { get; }
        public ITransport ConfigureChannels(ITransportConfiguration configuration)
        {
            foreach (var channelFactory in TransportChannelFactories)
            {
                channelFactory.Configuration = configuration;
            }

            return this;
        }

        public ITransport UseMiddleware(IMessageProcessorMiddleware middleware)
        {
            foreach (var channelFactory in TransportChannelFactories)
            {
                channelFactory.Middlewares.Add(middleware);
            }

            return this;
        }
    }

    public class NatsChannelFactory : ITransportChannelFactory
    {
        public NatsChannelFactory(INatsBusConfiguration configuration)
        {
            Configuration = configuration;
        }
        
        public ITransportConfiguration Configuration { get; set; }
        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();

        public IChannelReceiver Create(Type messageType, IEventSubscription subscription, IProcessingSettings processingSettings,
            IMessageSerializer serializer, IHostConfiguration configuration, IMessageProcessor processor)
        {

            var readerType = typeof(NatsQueueChannelReceiver<>).MakeGenericType(messageType);
            var reader = (IChannelReceiver) Activator.CreateInstance(readerType, processingSettings, serializer,
                configuration, processor, Configuration);

            return reader;
        }

        public bool CanCreate(Type messageType)
        {
            return typeof(INatsCommand).IsAssignableFrom(messageType);
        }
    }
    
    public interface INatsBusConfiguration : ITransportConfiguration
    {
        public Options Options { get; }
    }

    public class NatsBusConfiguration : INatsBusConfiguration
    {
        public NatsBusConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public string ConnectionString { get; }
        public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
        public Options Options { get; } = ConnectionFactory.GetDefaultOptions();
    }

    public interface INatsCommand : ICommand
    {
        
    }
}