using KnightBus.Core;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host
{
    internal class HostConfiguration : IHostConfiguration
    {
        public IDependencyInjection DependencyInjection { get; set; }
        public ILogger Log { get; set; }
    }
}