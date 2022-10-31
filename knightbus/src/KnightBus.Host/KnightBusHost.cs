using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Host.MessageProcessing;
using KnightBus.Microsoft.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host
{
    public class KnightBusHost : IHostedService
    {
        private IHostConfiguration _configuration;
        private MessageProcessorLocator _locator;

        public TimeSpan ShutdownGracePeriod { get; set; } = TimeSpan.FromMinutes(1);
        private readonly CancellationTokenSource _shutdownToken = new CancellationTokenSource();

        public KnightBusHost(IHostConfiguration configuration, IServiceProvider provider, ILogger<KnightBusHost> logger)
        {
            _configuration = configuration;
            configuration.DependencyInjection = new MicrosoftDependencyInjection(provider);
            configuration.Log = logger;
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
            ConsoleWriter.WriteLine("KnightBus starting");

            var transports = _configuration.DependencyInjection.GetInstances<ITransport>().ToArray();
            
            if (transports.Any())
            {
                _locator = new MessageProcessorLocator(_configuration,
                    transports.SelectMany(transport => transport.TransportChannelFactories).ToArray());
                var channelReceivers = _locator.CreateReceivers().ToList();
                ConsoleWriter.Write("Starting receivers [");
                foreach (var receiver in channelReceivers)
                {
                    await receiver.StartAsync(combinedToken.Token).ConfigureAwait(false);
                    Console.Write(".");
                }

                Console.WriteLine("]");
            }
            else
            {
                ConsoleWriter.WriteLine("No transports found");
            }

            foreach (var plugin in _configuration.DependencyInjection.GetInstances<IPlugin>())
            {
                await plugin.StartAsync(combinedToken.Token).ConfigureAwait(false);
            }
            ConsoleWriter.WriteLine("KnightBus started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _configuration.Log.LogInformation("KnightBus received stop signal, initiating shutdown... ");
            _shutdownToken.Cancel();
            await Task.Delay(ShutdownGracePeriod, cancellationToken).ConfigureAwait(false);
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