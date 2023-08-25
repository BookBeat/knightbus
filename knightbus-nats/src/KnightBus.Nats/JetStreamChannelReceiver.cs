using System;
using System.Threading;
using KnightBus.Core;
using KnightBus.Messages;
using NATS.Client;
using NATS.Client.JetStream;

namespace KnightBus.Nats
{
    public class JetStreamChannelReceiver<T> : NatsChannelReceiverBase<T>
        where T : class, IMessage
    {
        private readonly IJetStreamConfiguration _configuration;
        private readonly IEventSubscription _subscription;
        private const string DeliverGroup = "qg";


        public JetStreamChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IHostConfiguration hostConfiguration,
            IMessageProcessor processor, IJetStreamConfiguration configuration, IEventSubscription subscription)
            : base(settings, hostConfiguration, serializer, processor)
        {
            _configuration = configuration;
            _subscription = subscription;
        }

        public override IJetStreamPullSubscription Subscribe(IConnection connection, CancellationToken cancellationToken)
        {
            var jetStreamManagement = connection.CreateJetStreamManagementContext(_configuration.JetStreamOptions);
            string subscriptionName;
            if (_subscription is null)
                subscriptionName = DeliverGroup;
            else
                subscriptionName = _subscription.Name;

            var queueName = AutoMessageMapper.GetQueueName<T>();
            var streamName = $"{queueName}-stream";
            var subjects = $"{queueName}.>";
            var streamConfig = StreamConfiguration.Builder()
                .WithName(streamName)
                .WithSubjects(subjects)
                .WithStorageType(StorageType.Memory)
                .WithRetentionPolicy(RetentionPolicy.Interest)
                .Build();


            try
            {
                jetStreamManagement.UpdateStream(streamConfig);
            }
            catch (NATSJetStreamException e) when (e.ErrorCode == 404)
            {
                jetStreamManagement.AddStream(streamConfig);
            }

            var jetStream = connection.CreateJetStreamContext(_configuration.JetStreamOptions);

            var durable = $"{streamName}-{subscriptionName}-dr";

            var consumerConfig = ConsumerConfiguration.Builder()
                .WithDurable(durable)
                .WithAckPolicy(AckPolicy.Explicit)
                .WithAckWait((long)Math.Round(Settings.MessageLockTimeout.TotalMilliseconds, MidpointRounding.AwayFromZero))
                .WithMaxDeliver(Settings.DeadLetterDeliveryLimit)
                .BuildPullSubscribeOptions();

            return jetStream.PullSubscribe(subjects, consumerConfig);
        }
    }
}
