using System.Collections.Generic;
using KnightBus.Core.Singleton;

namespace KnightBus.Core
{
    /// <summary>
    /// Host specific configuration
    /// </summary>
    public interface IHostConfiguration
    {
        IList<IMessageProcessorMiddleware> Middlewares { get; }
        ISingletonLockManager SingletonLockManager { get; set; }
        IDependencyInjection DependencyInjection { get; set; }
        ILog Log { get; set; }
    }
}