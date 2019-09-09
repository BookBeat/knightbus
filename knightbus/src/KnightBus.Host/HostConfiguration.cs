using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Core.Singleton;

namespace KnightBus.Host
{
    internal class HostConfiguration : IHostConfiguration
    {
        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();
        public IList<IPlugin> Plugins { get; } = new List<IPlugin>();
        public ISingletonLockManager SingletonLockManager { get; set; }
        public IDependencyInjection DependencyInjection { get; set; }
        public ILog Log { get; set; } = new NoLogging();
    }
}