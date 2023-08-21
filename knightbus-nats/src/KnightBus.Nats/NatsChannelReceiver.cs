﻿using System.Threading;
using KnightBus.Core;
using KnightBus.Messages;
using NATS.Client;

namespace KnightBus.Nats
{
    public class NatsChannelReceiver<T> : NatsChannelReceiverBase<T> where T : class, IMessage
    {
        private readonly IEventSubscription _subscription;
        private const string CommandQueueGroup = "qg";

        public NatsChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IHostConfiguration hostConfiguration, IMessageProcessor processor, INatsConfiguration configuration, IEventSubscription subscription)
            : base(settings, hostConfiguration, serializer, processor)
        {
            Settings = settings;
            _subscription = subscription;
        }

        public override ISyncSubscription Subscribe(IConnection connection, CancellationToken cancellationToken)
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            if (_subscription is null)
                return connection.SubscribeSync(queueName, CommandQueueGroup);

            return connection.SubscribeSync(queueName, _subscription.Name);
        }
    }
}