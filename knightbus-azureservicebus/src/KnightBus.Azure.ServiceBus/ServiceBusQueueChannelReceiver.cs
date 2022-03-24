using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core;
using KnightBus.Messages;

[assembly: InternalsVisibleTo("BB.Common.KnightBus.Tests.Unit")]
namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusQueueChannelReceiver<T> : ServiceBusChannelReceiverBase<T>
        where T : class, ICommand
    {
        public ServiceBusQueueChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer,
            IServiceBusConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor) :
            base(settings, serializer, configuration, hostConfiguration, processor)
        {

        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            Client = await ClientFactory.GetReceiverClient<T>(new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = Settings.MessageLockTimeout,
                MaxConcurrentCalls = Settings.MaxConcurrentCalls,
                PrefetchCount = Settings.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock

            }).ConfigureAwait(false);

            var queueName = AutoMessageMapper.GetQueueName<T>();

            //TODO: optimistic queue check
            if (!await ManagementClient.QueueExistsAsync(queueName, CancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var serviceBusCreationOptions = GetServiceBusCreationOptions();

                    await ManagementClient.CreateQueueAsync(new CreateQueueOptions(queueName)
                    {
                        EnablePartitioning = serviceBusCreationOptions.EnablePartitioning,
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations

                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException e)
                {
                    Log.Error(e, "Failed to create queue {QueueName}", queueName);
                    throw;
                }
            }
            DeadLetterLimit = Settings.DeadLetterDeliveryLimit;

            Client.ProcessMessageAsync += ClientOnProcessMessageAsync;
            Client.ProcessErrorAsync += ClientOnProcessErrorAsync;

            await Client.StartProcessingAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable 4014
            // ReSharper disable once MethodSupportsCancellation
            Task.Run(async () =>
            {
                CancellationToken.WaitHandle.WaitOne();
                try
                {
                    Log.Information($"Closing ServiceBus channel receiver for {typeof(T).Name}");
                    await Client.CloseAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    //Swallow
                }
            }, cancellationToken);


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
