using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Nats.Messages;
using Microsoft.Extensions.Logging;
using NATS.Client;
using NATS.Client.JetStream;

namespace KnightBus.Nats
{
    public class JetStreamChannelReceiver<T> : NatsChannelReceiverBase<T>
        where T : class, IMessage
    {
        private readonly IJetStreamConfiguration _configuration;
        private readonly IEventSubscription _subscription;
        private const string CommandQueueGroup = "qg";
        

        public JetStreamChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IHostConfiguration hostConfiguration, IMessageProcessor processor, IJetStreamConfiguration configuration, IEventSubscription subscription)
            : base(settings, hostConfiguration, serializer, processor)
        {
            _configuration = configuration;
            _subscription = subscription;
        }

        public override ISyncSubscription Subscribe(IConnection connection, CancellationToken cancellationToken)
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var streamName = $"{queueName}-stream";
            var streamConfig = StreamConfiguration.Builder()
                .WithName(streamName)
                .WithSubjects(queueName)
                .WithStorageType(StorageType.File)
                .WithRetentionPolicy(RetentionPolicy.WorkQueue)
                .Build();

            connection.CreateJetStreamManagementContext(_configuration.JetStreamOptions)
                .AddStream(streamConfig);

            var jetStream = connection.CreateJetStreamContext(_configuration.JetStreamOptions);

            var durable = $"{streamName}_consumer";
            var consumerConfig = ConsumerConfiguration.Builder()
                .WithDurable(durable)
                .WithAckPolicy(AckPolicy.Explicit)
                .WithFilterSubject(queueName)
                .Build();

            var options = PushSubscribeOptions.Builder().WithConfiguration(consumerConfig).Build();

            if (_subscription is null)
                return jetStream.PushSubscribeSync(queueName, CommandQueueGroup, options);
            
            return jetStream.PushSubscribeSync(queueName, _subscription.Name, options);
        }
    }
}
