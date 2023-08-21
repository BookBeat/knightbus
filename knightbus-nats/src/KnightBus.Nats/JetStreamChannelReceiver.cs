using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Nats.Messages;
using Microsoft.Extensions.Logging;
using NATS.Client;
using NATS.Client.Internals;
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
            var streamName = AutoMessageMapper.GetQueueName<T>();
            var streamConfig = StreamConfiguration.Builder()
                .WithName(streamName)
                .WithSubjects(streamName)
                .WithStorageType(StorageType.File)
                .WithRetentionPolicy(RetentionPolicy.WorkQueue)
                .Build();

            var jetStreamManagement = connection.CreateJetStreamManagementContext(_configuration.JetStreamOptions);
            jetStreamManagement.AddStream(streamConfig);

            var jetStream = connection.CreateJetStreamContext(_configuration.JetStreamOptions);

            var durable = $"{streamName}_consumer";
            var consumerConfig = ConsumerConfiguration.Builder()
                .WithDurable(durable)
                .WithAckPolicy(AckPolicy.Explicit)
                .WithMaxDeliver(Settings.DeadLetterDeliveryLimit)
                .WithFilterSubject(streamName) //Needed?
                .BuildPushSubscribeOptions();

            //jetStreamManagement.AddOrUpdateConsumer(streamName, consumerConfig.ConsumerConfiguration);


            if (_subscription is null)
                return jetStream.PushSubscribeSync(streamName, CommandQueueGroup, consumerConfig);

            return jetStream.PushSubscribeSync(streamName, _subscription.Name, consumerConfig);
        }
    }
}
