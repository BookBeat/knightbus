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


        public JetStreamChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IHostConfiguration hostConfiguration, IMessageProcessor processor, IJetStreamConfiguration configuration, IEventSubscription subscription)
            : base(settings, hostConfiguration, serializer, processor)
        {
            _configuration = configuration;
            _subscription = subscription;
        }

        public override ISyncSubscription Subscribe(IConnection connection, CancellationToken cancellationToken)
        {
            string deliveryGroup;
            if (_subscription is null)
                deliveryGroup = DeliverGroup;
            else
                deliveryGroup = _subscription.Name;

            var streamName = AutoMessageMapper.GetQueueName<T>();
            var streamConfig = StreamConfiguration.Builder()
                .WithName(streamName)
                .WithSubjects(streamName)
                .WithStorageType(StorageType.File)
                .WithRetentionPolicy(RetentionPolicy.Interest)
                .Build();

            var topicName = $"{streamName}-{deliveryGroup}";

            var source = new Source.SourceBuilder().WithName(streamName).WithFilterSubject(streamName).Build();
            var topicConfig = StreamConfiguration.Builder()
                .WithSources(source)
                .WithName(topicName)
                .WithSubjects(deliveryGroup)
                .WithStorageType(StorageType.File)
                .WithRetentionPolicy(RetentionPolicy.WorkQueue)
                .Build();

            var jetStreamManagement = connection.CreateJetStreamManagementContext(_configuration.JetStreamOptions);
            var s = jetStreamManagement.AddStream(streamConfig);

            var t = jetStreamManagement.AddStream(topicConfig);



            var jetStream = connection.CreateJetStreamContext(_configuration.JetStreamOptions);

            var durable = $"{streamName}_{deliveryGroup}-dr";
            var consumerConfig = ConsumerConfiguration.Builder()
                .WithDurable(durable)
                .WithAckPolicy(AckPolicy.Explicit)
                .WithMaxDeliver(Settings.DeadLetterDeliveryLimit)
                .BuildPullSubscribeOptions();

            //jetStreamManagement.AddOrUpdateConsumer(streamName, consumerConfig.ConsumerConfiguration);


            return jetStream.PullSubscribe(deliveryGroup, consumerConfig);
        }
    }
}
