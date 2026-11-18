using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;

namespace KnightBus.Azure.ServiceBus;

internal class ServiceBusTopicChannelReceiver<T>
    : ServiceBusChannelReceiverBase<T>,
        IChannelReceiver
    where T : class, IEvent
{
    private readonly IEventSubscription<T> _subscription;
    private readonly string? _forwardingQueue;
    private bool _forwardingEnsured;

    public ServiceBusTopicChannelReceiver(
        IProcessingSettings settings,
        IMessageSerializer serializer,
        IEventSubscription<T> subscription,
        IServiceBusConfiguration configuration,
        IHostConfiguration hostConfiguration,
        IMessageProcessor processor
    )
        : base(settings, serializer, configuration, hostConfiguration, processor)
    {
        _subscription = subscription;
        _forwardingQueue = SubscriptionForwarding.GetForwardingQueue(
            configuration,
            AutoMessageMapper.GetQueueName<T>(),
            subscription
        );
    }

    protected override async Task<ServiceBusProcessor> CreateClient(
        CancellationToken cancellationToken
    )
    {
        var options = new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxAutoLockRenewalDuration = Settings.MessageLockTimeout,
            MaxConcurrentCalls = Settings.MaxConcurrentCalls,
            PrefetchCount = Settings.PrefetchCount,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
        };

        if (_forwardingQueue is null)
        {
            return await ClientFactory
                .GetReceiverClient<T, IEventSubscription<T>>(_subscription, options)
                .ConfigureAwait(false);
        }

        //An existing subscription is not missing, so it would never reach CreateMessagingEntity.
        //Point it at the queue once per receiver before reading from the queue
        if (!_forwardingEnsured)
        {
            await EnsureForwarding(_forwardingQueue, cancellationToken).ConfigureAwait(false);
            _forwardingEnsured = true;
        }
        return await ClientFactory
            .GetReceiverClient(_forwardingQueue, options)
            .ConfigureAwait(false);
    }

    private async Task EnsureForwarding(string queue, CancellationToken cancellationToken)
    {
        var topicName = AutoMessageMapper.GetQueueName<T>();
        try
        {
            await SubscriptionForwarding
                .EnsureAsync(
                    ManagementClient,
                    topicName,
                    _subscription.Name,
                    queue,
                    GetServiceBusCreationOptions(),
                    Log,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (ServiceBusException e)
        {
            Log.LogError(
                e,
                "Failed to forward subscription {TopicName} {SubscriptionName} to queue {QueueName}",
                topicName,
                _subscription.Name,
                queue
            );
            throw;
        }
    }

    protected override async Task CreateMessagingEntity(CancellationToken cancellationToken)
    {
        if (_forwardingQueue is not null)
        {
            await EnsureForwarding(_forwardingQueue, cancellationToken).ConfigureAwait(false);
            return;
        }

        var topicName = AutoMessageMapper.GetQueueName<T>();
        var serviceBusCreationOptions = GetServiceBusCreationOptions();
        try
        {
            await ManagementClient
                .CreateTopicAsync(
                    new CreateTopicOptions(topicName)
                    {
                        EnablePartitioning = serviceBusCreationOptions.EnablePartitioning,
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations,
                        SupportOrdering = serviceBusCreationOptions.SupportOrdering,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (ServiceBusException e)
            when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
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
            await ManagementClient
                .CreateSubscriptionAsync(
                    new CreateSubscriptionOptions(topicName, _subscription.Name)
                    {
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (ServiceBusException e)
            when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            //Swallow
        }
        catch (ServiceBusException e)
        {
            Log.LogError(
                e,
                "Failed to create subscription {TopicName} {SubscriptionName}",
                topicName,
                _subscription.Name
            );
            throw;
        }
    }
}
