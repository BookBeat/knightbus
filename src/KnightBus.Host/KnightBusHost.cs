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

        public KnightBusHost Configure(Func<IHostConfiguration,IHostConfiguration> configuration)
        {
            _configuration = configuration(_configuration);
            return this;
        }
        /// <summary>
        /// Starts the bus and wires all listeners
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            if(!_transports.Any()) throw new TransportMissingException("No transports configured");

            _locator = new MessageProcessorLocator(_configuration, _transports.SelectMany(transport=> transport.TransportFactories).ToArray());
            var queueReaders = _locator.Locate();
            foreach (var queueReader in queueReaders)
            {
                await queueReader.StartAsync();
            }
        }

        public async Task StartAndBlockAsync()
        {
            await StartAsync().ConfigureAwait(false);
            var token = new CancellationToken();
            token.WaitHandle.WaitOne();
        }
    }
}