using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core;

/// <summary>
/// A plugin that needs to stop cleanly when the host shuts down.
/// </summary>
public interface IStoppablePlugin : IPlugin
{
    /// <summary>
    /// Called when the host begins shutting down. Stop accepting new work immediately;
    /// the returned task should complete when the plugin's in-flight work has finished.
    /// The host waits for it, bounded by the shutdown grace period.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);
}
