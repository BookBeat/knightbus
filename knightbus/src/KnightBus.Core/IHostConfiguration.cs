using System.Collections.Generic;
using KnightBus.Core.Singleton;

namespace KnightBus.Core
{
    public interface IHostConfiguration
    {
        IList<IMessageProcessorMiddleware> Middlewares { get; }
        ISingletonLockManager SingletonLockManager { get; set; }
        IMessageProcessorProvider MessageProcessorProvider { get; set; }
        ILog Log { get; set; }
    }
}