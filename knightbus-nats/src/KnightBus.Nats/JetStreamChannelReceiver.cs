using System;
using System.Threading;
using KnightBus.Core;
using KnightBus.Messages;
using NATS.Client;
using NATS.Client.JetStream;

namespace KnightBus.Nats
{
    public static class JetStreamHelpers
    {
        public static string GetRootDeadletterSubject(string name) => $"dl-{name}";

        public static string GetRootSubject(string subject) => subject.IndexOf('.') < 0 ? subject : subject[..subject.IndexOf('.')];
        public static string ConvertToDeadletterSubject(string subject)
        {
            var root = GetRootSubject(subject);
            return subject.Replace(root, GetRootDeadletterSubject(root));
        }


    }
    public class JetStreamChannelReceiver<T> : NatsChannelReceiverBase<T>
        where T : class, IMessage
    {
        private readonly IJetStreamConfiguration _configuration;
        private readonly IEventSubscription _subscription;
        private const string CommandDeliverGroup = "qg";


        public JetStreamChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IHostConfiguration hostConfiguration,
            IMessageProcessor processor, IJetStreamConfiguration configuration, IEventSubscription subscription)
            : base(settings, hostConfiguration, serializer, processor)
        {
            _configuration = configuration;
            _subscription = subscription;
        }

        public override IJetStreamPushSyncSubscription Subscribe(IConnection connection, CancellationToken cancellationToken)
        {
            var jetStreamManagement = connection.CreateJetStreamManagementContext(_configuration.JetStreamOptions);
            string deliverGroup;
            if (_subscription is null)
                deliverGroup = CommandDeliverGroup;
            else
                deliverGroup = _subscription.Name;

            var queueName = JetStreamHelpers.GetRootSubject(AutoMessageMapper.GetQueueName<T>());
            var streamName = $"{queueName}-stream";
            var streamSubject = $"{queueName}.>";

            var deadletterName = JetStreamHelpers.GetRootDeadletterSubject(queueName);
            var deadLetterStreamName = $"{deadletterName}-stream";
            var deadletterSubject = $"{deadletterName}.>";

            var streamConfig = StreamConfiguration.Builder()
                .WithName(streamName)
                .WithSubjects(streamSubject)
                .WithStorageType(StorageType.Memory)
                .WithRetentionPolicy(RetentionPolicy.Interest)
                .Build();

            UpsertStream(jetStreamManagement, streamConfig);

            var dlStreamConfig = StreamConfiguration.Builder()
                .WithName(deadLetterStreamName)
                .WithSubjects(deadletterSubject)
                .WithStorageType(StorageType.Memory)
                .WithRetentionPolicy(RetentionPolicy.WorkQueue)
                .Build();

            UpsertStream(jetStreamManagement, dlStreamConfig);

            var jetStream = connection.CreateJetStreamContext(_configuration.JetStreamOptions);

            var durable = $"{streamName}-{deliverGroup}-dr";

            var subscribeOptions = ConsumerConfiguration.Builder()
                .WithDurable(durable)
                .WithAckPolicy(AckPolicy.Explicit)
                .WithAckWait((long)Math.Round(Settings.MessageLockTimeout.TotalMilliseconds, MidpointRounding.AwayFromZero))
                .BuildPushSubscribeOptions();

            return jetStream.PushSubscribeSync(streamSubject, deliverGroup, subscribeOptions);
        }

        private void UpsertStream(IJetStreamManagement jetStreamManagement, StreamConfiguration streamConfiguration)
        {
            try
            {
                jetStreamManagement.UpdateStream(streamConfiguration);
            }
            catch (NATSJetStreamException e) when (e.ErrorCode == 404)
            {
                jetStreamManagement.AddStream(streamConfiguration);
            }
        }
    }
}
