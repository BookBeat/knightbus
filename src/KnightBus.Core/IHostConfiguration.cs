using System;
using Microsoft.Extensions.Logging;

namespace KnightBus.Core;

/// <summary>
/// Host specific configuration
/// </summary>
public interface IHostConfiguration
{
    IDependencyInjection DependencyInjection { get; set; }
    ILogger Log { get; set; }
    TimeSpan ShutdownGracePeriod { get; set; }
}
