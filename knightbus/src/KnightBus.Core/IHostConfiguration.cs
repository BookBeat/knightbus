using System;
using System.Collections.Generic;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;

namespace KnightBus.Core
{
    /// <summary>
    /// Host specific configuration
    /// </summary>
    public interface IHostConfiguration
    {
        IList<ITransport> Transports { get; }
        IList<IMessageProcessorMiddleware> Middlewares { get; }
        IList<IPlugin> Plugins { get; } 
        ISingletonLockManager SingletonLockManager { get; set; }
        IDependencyInjection DependencyInjection { get; set; }
        ILogger Log { get; set; }
    }
}