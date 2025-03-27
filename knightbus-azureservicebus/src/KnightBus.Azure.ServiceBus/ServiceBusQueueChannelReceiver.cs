using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("BB.Common.KnightBus.Tests.Unit")]

namespace KnightBus.Azure.ServiceBus;

internal class ServiceBusQueueChannelReceiver<T> : ServiceBusChannelReceiverBase<T>
    where T : class, ICommand
{
    public ServiceBusQueueChannelReceiver(
        IProcessingSettings settings,
        IMessageSerializer serializer,
        IServiceBusConfiguration configuration,
        IHostConfiguration hostConfiguration,
        IMessageProcessor processor
    )
        : base(settings, serializer, configuration, hostConfiguration, processor) { }

    protected override Task<ServiceBusProcessor> CreateClient(CancellationToken cancellationToken)
    {
        return ClientFactory.GetReceiverClient<T>(
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = Settings.MessageLockTimeout,
                MaxConcurrentCalls = Settings.MaxConcurrentCalls,
                PrefetchCount = Settings.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
            }
        );
    }

    protected override async Task CreateMessagingEntity(CancellationToken cancellationToken)
    {
        var queueName = AutoMessageMapper.GetQueueName<T>();
        try
        {
            var serviceBusCreationOptions = GetServiceBusCreationOptions();

            await ManagementClient
                .CreateQueueAsync(
                    new CreateQueueOptions(queueName)
                    {
                        EnablePartitioning = serviceBusCreationOptions.EnablePartitioning,
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
            Log.LogError(e, "Failed to create queue {QueueName}", queueName);
            throw;
        }
    }
}
