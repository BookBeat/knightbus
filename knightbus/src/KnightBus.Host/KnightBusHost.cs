using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Host
{
    public class KnightBusHost
    {
        private IHostConfiguration _configuration;
        private MessageProcessorLocator _locator;
        private readonly List<ITransport> _transports = new List<ITransport>();

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
            ConsoleWriter.WriteLine("KnightBus starting");
            if (_transports.Any())
            {
                _locator = new MessageProcessorLocator(_configuration, _transports.SelectMany(transport => transport.TransportChannelFactories).ToArray());
                var channelReceivers = _locator.Locate().ToList();
                ConsoleWriter.Write("Starting receivers [");
                foreach (var queueReader in channelReceivers)
                {
                    await queueReader.StartAsync(cancellationToken).ConfigureAwait(false);
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
                    await plugin.StartAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                ConsoleWriter.WriteLine("No plugins found");
            }

            ConsoleWriter.WriteLine("KnightBus started");
        }

        public async Task StartAndBlockAsync(CancellationToken cancellationToken)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.WaitHandle.WaitOne();
        }
    }
}