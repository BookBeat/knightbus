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
        private const string CommandQueueGroup = "qg";


        public JetStreamChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IHostConfiguration hostConfiguration, IMessageProcessor processor, IJetStreamConfiguration configuration, IEventSubscription subscription)
            : base(settings, hostConfiguration, serializer, processor)
        {
            _configuration = configuration;
            _subscription = subscription;
        }

        public override ISyncSubscription Subscribe(IConnection connection, CancellationToken cancellationToken)
        {
            string subscription;
            if (_subscription is null)
                subscription = CommandQueueGroup;
            else
                subscription = _subscription.Name;

            var streamName = AutoMessageMapper.GetQueueName<T>();
            var streamConfig = StreamConfiguration.Builder()
                .WithName(streamName)
                //.WithSubjects(subscription)
                .WithStorageType(StorageType.File)
                .WithRetentionPolicy(RetentionPolicy.WorkQueue)
                .Build();

            var jetStreamManagement = connection.CreateJetStreamManagementContext(_configuration.JetStreamOptions);
            var a = jetStreamManagement.AddStream(streamConfig);
            a.Config.Subjects.Add(subscription);

            var jetStream = connection.CreateJetStreamContext(_configuration.JetStreamOptions);

            var durable = $"{streamName}_{subscription}";
            var consumerConfig = ConsumerConfiguration.Builder()
                .WithDurable(durable)
                .WithDeliverGroup(subscription)
                .WithDeliverSubject(subscription)
                .WithAckPolicy(AckPolicy.Explicit)
                .WithMaxDeliver(Settings.DeadLetterDeliveryLimit)
                .WithFilterSubject(streamName) //Needed?

                .BuildPushSubscribeOptions();

            jetStreamManagement.AddOrUpdateConsumer(streamName, consumerConfig.ConsumerConfiguration);


            if (_subscription is null)
                return jetStream.PushSubscribeSync(streamName, CommandQueueGroup, consumerConfig);

            return jetStream.PushSubscribeSync(streamName, _subscription.Name, consumerConfig);
        }
    }
}
