using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core;

/// <summary>
/// A receiver that can stop taking new messages while the ones it is already processing carry on.
/// Used to hand a singleton lock over without cancelling work that is about to finish.
/// </summary>
public interface IDrainableChannelReceiver : IChannelReceiver
{
    /// <summary>
    /// Stops fetching new messages and completes when the messages already being processed have
    /// finished. Those messages are not cancelled; cancelling <paramref name="cancellationToken"/>
    /// only stops the wait.
    /// </summary>
    Task StopFetchingAsync(CancellationToken cancellationToken);
}
