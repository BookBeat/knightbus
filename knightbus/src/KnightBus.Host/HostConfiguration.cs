using System.Collections.Generic;
using KnightBus.Core;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host
{
    internal class HostConfiguration : IHostConfiguration
    {
        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();
        public IDependencyInjection DependencyInjection { get; set; }
        public ILogger Log { get; set; }
    }
}