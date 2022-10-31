using System.Collections.Generic;
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
        IDependencyInjection DependencyInjection { get; set; }
        ILogger Log { get; set; }
    }
}