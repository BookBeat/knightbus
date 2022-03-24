using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus
{
    internal abstract class ServiceBusChannelReceiverBase<T> : IChannelReceiver
        where T : class, IMessage
    {
        protected IClientFactory ClientFactory;
        public IProcessingSettings Settings { get; set; }
        protected readonly ILog Log;
        protected readonly IMessageSerializer Serializer;
        protected readonly IServiceBusConfiguration Configuration;
        protected readonly IHostConfiguration HostConfiguration;
        protected readonly IMessageProcessor Processor;
        protected int DeadLetterLimit;
        protected ServiceBusProcessor Client;
        protected ServiceBusAdministrationClient ManagementClient;
        protected CancellationToken CancellationToken;
        protected CancellationTokenSource RestartTaskCancellation;
        protected DateTimeOffset LastProcess;
        protected readonly object LastProcessLocker = new object();

        protected ServiceBusChannelReceiverBase(IProcessingSettings settings, IMessageSerializer serializer, IServiceBusConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            Serializer = serializer;
            Configuration = configuration;
            HostConfiguration = hostConfiguration;
            Processor = processor;
            Settings = settings;
            Log = hostConfiguration.Log;
            //new client factory per ServiceBusQueueChannelReceiver means a separate communication channel per reader instead of a shared
            ClientFactory = new ClientFactory(configuration.ConnectionString);
            ManagementClient = new ServiceBusAdministrationClient(configuration.ConnectionString);
            LastProcess = DateTimeOffset.UtcNow;
        }

        public abstract Task StartAsync(CancellationToken cancellationToken);

        protected async Task CheckIdleTime(TimeSpan idleTimeout)
        {
            lock (LastProcessLocker)
            {
                var timeSinceLastProcessedMessage = DateTimeOffset.UtcNow - LastProcess;

                if (timeSinceLastProcessedMessage < idleTimeout) return;

                Log.Information($"Last message was processed at: {LastProcess} (maximum allowed idle timespan: {idleTimeout}), restarting");
            }

            await RestartAsync().ConfigureAwait(false);
        }

        protected async Task RestartAsync()
        {
            try
            {
                Log.Information($"Restarting {typeof(T).Name}");

                await Client.StopProcessingAsync(CancellationToken).ConfigureAwait(false);
                Client.ProcessMessageAsync -= ClientOnProcessMessageAsync;
                Client.ProcessErrorAsync -= ClientOnProcessErrorAsync;

                RestartTaskCancellation.Cancel();

                await ClientFactory.DisposeAsync().ConfigureAwait(false);

                ClientFactory = new ClientFactory(Configuration.ConnectionString);
                ManagementClient = new ServiceBusAdministrationClient(Configuration.ConnectionString);

                await StartAsync(CancellationToken).ConfigureAwait(false);
                Log.Information($"Successfully restarted {typeof(T).Name}");
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to restart {typeof(T).Name}");
                await RestartAsync().ConfigureAwait(false);
            }
        }

        protected Task ClientOnProcessErrorAsync(ProcessErrorEventArgs arg)
        {
            if (!(arg.Exception is OperationCanceledException))
            {
                Log.Error(arg.Exception, $"{typeof(T).Name}");
            }
            return Task.CompletedTask;
        }

        protected async Task ClientOnProcessMessageAsync(ProcessMessageEventArgs arg)
        {
            try
            {
                var stateHandler = new ServiceBusMessageStateHandler<T>(arg, Serializer, DeadLetterLimit, HostConfiguration.DependencyInjection);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, arg.CancellationToken);
                await Processor.ProcessAsync(stateHandler, cts.Token).ConfigureAwait(false);
                lock (LastProcessLocker)
                {
                    LastProcess = DateTimeOffset.UtcNow;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "ServiceBus OnMessage Failed");
            }
        }

        protected IServiceBusCreationOptions GetServiceBusCreationOptions()
        {
            var queueMapping = AutoMessageMapper.GetMapping<T>();
            // ReSharper disable once SuspiciousTypeConversion.Global
            var creationOptions = queueMapping as IServiceBusCreationOptions;

            return creationOptions ?? Configuration.DefaultCreationOptions;
        }
    }
}
