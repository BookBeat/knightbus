using System;
using KnightBus.Core;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host;

internal class HostConfiguration : IHostConfiguration
{
    public IDependencyInjection DependencyInjection { get; set; } = null!;
    public ILogger Log { get; set; } = null!;
    public TimeSpan ShutdownGracePeriod { get; set; } = TimeSpan.FromSeconds(30);
}
