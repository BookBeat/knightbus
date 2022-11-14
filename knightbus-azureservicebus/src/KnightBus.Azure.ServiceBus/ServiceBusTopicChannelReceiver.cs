using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;

namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusTopicChannelReceiver<T> : ServiceBusChannelReceiverBase<T>, IChannelReceiver
        where T : class, IEvent
    {
        private readonly IEventSubscription<T> _subscription;

        public ServiceBusTopicChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer,
            IEventSubscription<T> subscription, IServiceBusConfiguration configuration,
            IHostConfiguration hostConfiguration, IMessageProcessor processor) : base(settings, serializer,
            configuration, hostConfiguration, processor)
        {
            _subscription = subscription;
        }

        protected override Task<ServiceBusProcessor> CreateClient(CancellationToken cancellationToken)
        {
            return ClientFactory.GetReceiverClient<T, IEventSubscription<T>>(_subscription, new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = Settings.MessageLockTimeout,
                MaxConcurrentCalls = Settings.MaxConcurrentCalls,
                PrefetchCount = Settings.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock

            });
        }

        protected override async Task CreateMessagingEntity(CancellationToken cancellationToken)
        {
            var topicName = AutoMessageMapper.GetQueueName<T>();
            var serviceBusCreationOptions = GetServiceBusCreationOptions();
            try
            {
                await ManagementClient.CreateTopicAsync(new CreateTopicOptions(topicName)
                {
                    EnablePartitioning = serviceBusCreationOptions.EnablePartitioning,
                    EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations,
                    SupportOrdering = serviceBusCreationOptions.SupportOrdering

                }, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                //Swallow
            }
            catch (ServiceBusException e)
            {
                Log.LogError(e, "Failed to create topic {TopicName}", topicName);
                throw;
            }

            try
            {
                await ManagementClient.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, _subscription.Name)
                {
                    EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException e) when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                //Swallow
            }
            catch (ServiceBusException e)
            {
                Log.LogError(e, "Failed to create subscription {TopicName} {SubscriptionName}", topicName, _subscription.Name);
                throw;
            }
        }
    }
}
