using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core;
using KnightBus.Messages;

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

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            Client = await ClientFactory.GetReceiverClient<T, IEventSubscription<T>>(_subscription, new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = Settings.MessageLockTimeout,
                MaxConcurrentCalls = Settings.MaxConcurrentCalls,
                PrefetchCount = Settings.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock

            }).ConfigureAwait(false);


            var topicName = AutoMessageMapper.GetQueueName<T>();

            if (!await ManagementClient.TopicExistsAsync(topicName, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var serviceBusCreationOptions = GetServiceBusCreationOptions();

                    await ManagementClient.CreateTopicAsync(new CreateTopicOptions(topicName)
                    {
                        EnablePartitioning = serviceBusCreationOptions.EnablePartitioning,
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations,
                        SupportOrdering = serviceBusCreationOptions.SupportOrdering

                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException e)
                {
                    Log.Error(e, "Failed to create topic {TopicName}", topicName);
                    throw;
                }
            }
            if (!await ManagementClient.SubscriptionExistsAsync(topicName, _subscription.Name, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var serviceBusCreationOptions = GetServiceBusCreationOptions();

                    await ManagementClient.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, _subscription.Name)
                    {
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException e)
                {
                    Log.Error(e, "Failed to create subscription {TopicName} {SubscriptionName}", topicName, _subscription.Name);
                    throw;
                }
            }

            DeadLetterLimit = Settings.DeadLetterDeliveryLimit;

            Client.ProcessMessageAsync += ClientOnProcessMessageAsync;
            Client.ProcessErrorAsync += ClientOnProcessErrorAsync;

            await Client.StartProcessingAsync(cancellationToken);

#pragma warning disable 4014
            // ReSharper disable once MethodSupportsCancellation
            Task.Run(async () =>
            {
                CancellationToken.WaitHandle.WaitOne();
                //Cancellation requested
                try
                {
                    Log.Information($"Closing ServiceBus channel receiver for {typeof(T).Name}");
                    await Client.CloseAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    //Swallow
                }
            });

            RestartTaskCancellation = new CancellationTokenSource();
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (Settings is IRestartTransportOnIdle restartOnIdle)
            {
                Log.Information($"Starting idle timeout check for {typeof(T).Name} with maximum allowed idle timespan: {restartOnIdle.IdleTimeout}");
                Task.Run(async () =>
                {
                    while (!RestartTaskCancellation.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), RestartTaskCancellation.Token).ConfigureAwait(false);
                        await CheckIdleTime(restartOnIdle.IdleTimeout).ConfigureAwait(false);
                    }
                }, RestartTaskCancellation.Token);
            }
#pragma warning restore 4014
        }
    }
}
