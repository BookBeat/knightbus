using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Host
{
    public class KnightBusHost : IHostedService
    {
        private IHostConfiguration _configuration;
        private MessageProcessorLocator _locator;
        private readonly List<ITransport> _transports = new List<ITransport>();
        public TimeSpan ShutdownGracePeriod { get; set; }= TimeSpan.FromMinutes(1);
        private CancellationTokenSource _shutdownToken = new CancellationTokenSource();

        public KnightBusHost()
        {
            _configuration = new HostConfiguration();
        }

        public KnightBusHost UseTransport(ITransport transport)
        {
            _transports.Add(transport);
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
                
            if (_transports.Any())
            {
                _locator = new MessageProcessorLocator(_configuration, _transports.SelectMany(transport => transport.TransportChannelFactories).ToArray());
                var channelReceivers = _locator.Locate().ToList();
                ConsoleWriter.Write("Starting receivers [");
                foreach (var queueReader in channelReceivers)
                {
                    await queueReader.StartAsync(combinedToken.Token).ConfigureAwait(false);
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
            _configuration.Log.Information("KnightBus received stop signal, initiating shutdown... ");
            _shutdownToken.Cancel();
            await Task.Delay(ShutdownGracePeriod, cancellationToken);
            _configuration.Log.Information("KnightBus received stopped");
        }

        public async Task StartAndBlockAsync(CancellationToken cancellationToken)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.WaitHandle.WaitOne();
        }
    }
}