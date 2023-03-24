using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host.MessageProcessing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host
{
    public class KnightBusHost : IHostedService
    {
        private IHostConfiguration _configuration;
        private MessageProcessorLocator _locator;
        private readonly CancellationTokenSource _shutdownToken = new CancellationTokenSource();

        public KnightBusHost(IHostConfiguration configuration, IServiceProvider provider, ILogger<KnightBusHost> logger)
        {
            configuration.DependencyInjection = new MicrosoftDependencyInjection(provider);
            configuration.Log = logger;
            _configuration = configuration;
        }

        public KnightBusHost Configure(Func<IHostConfiguration, IHostConfiguration> configuration)
        {
            _configuration = configuration(_configuration);
            return this;
        }

        /// <summary>
        /// Starts the bus and wires all listeners
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownToken.Token);
            _configuration.Log.LogInformation("KnightBus starting");

            var transports = _configuration.DependencyInjection.GetInstances<ITransport>().ToArray();

            if (transports.Any())
            {
                _locator = new MessageProcessorLocator(_configuration,
                    transports.SelectMany(transport => transport.TransportChannelFactories).ToArray());
                var channelReceivers = _locator.CreateReceivers().ToList();
                _configuration.Log.LogInformation("Starting receivers");
                foreach (var receiver in channelReceivers)
                {
                    _configuration.Log.LogInformation("Starting receiver {ReceiverType}", receiver.GetType());
                    await receiver.StartAsync(combinedToken.Token).ConfigureAwait(false);
                }

                _configuration.Log.LogInformation("Finished starting receivers");
            }
            else
            {
                _configuration.Log.LogInformation("No transports found");
            }

            foreach (var plugin in _configuration.DependencyInjection.GetInstances<IPlugin>())
            {
                await plugin.StartAsync(combinedToken.Token).ConfigureAwait(false);
            }
            _configuration.Log.LogInformation("KnightBus started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _configuration.Log.LogInformation("KnightBus received stop signal, initiating shutdown... ");
            _shutdownToken.Cancel();
            try
            {
                await Task.Delay(_configuration.ShutdownGracePeriod, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                //Swallow
            }
            _configuration.Log.LogInformation("KnightBus shutdown completed");
            _shutdownToken.Dispose();
        }

        public async Task StartAndBlockAsync(CancellationToken cancellationToken)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.WaitHandle.WaitOne();
        }
    }
}
