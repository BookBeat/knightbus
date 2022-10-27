using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Host.MessageProcessing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host
{
    public class KnightBusHost : IHostedService
    {
        private readonly ILogger<KnightBusHost> _logger;
        private IHostConfiguration _configuration;
        private MessageProcessorLocator _locator;
        
        public TimeSpan ShutdownGracePeriod { get; set; }= TimeSpan.FromMinutes(1);
        private readonly CancellationTokenSource _shutdownToken = new CancellationTokenSource();

        public KnightBusHost(IHostConfiguration configuration, ILogger<KnightBusHost> logger)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public KnightBusHost UseTransport(ITransport transport)
        {
            _configuration.Transports.Add(transport);
            return this;
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
            if(_configuration.DependencyInjection == null)
                throw new DependencyInjectionMissingException();

            if(_configuration.DependencyInjection is IIsolatedDependencyInjection isolated)
                isolated.Build();
                
            if (_configuration.Transports.Any())
            {
                _locator = new MessageProcessorLocator(_configuration, _configuration.Transports.SelectMany(transport => transport.TransportChannelFactories).ToArray());
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
            
            if (_configuration.Plugins.Any())
            {
                foreach (var plugin in _configuration.Plugins)
                {
                    await plugin.StartAsync(combinedToken.Token).ConfigureAwait(false);
                }
            }
            else
            {
                ConsoleWriter.WriteLine("No plugins found");
            }

            ConsoleWriter.WriteLine("KnightBus started");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _configuration.Log.LogInformation("KnightBus received stop signal, initiating shutdown... ");
            _shutdownToken.Cancel();
            await Task.Delay(ShutdownGracePeriod, cancellationToken).ConfigureAwait(false);
            _configuration.Log.LogInformation("KnightBus received stopped");
            _shutdownToken.Dispose();
        }

        public async Task StartAndBlockAsync(CancellationToken cancellationToken)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.WaitHandle.WaitOne();
        }
    }
}