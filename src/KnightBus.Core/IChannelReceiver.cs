using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core;

/// <summary>
/// Receives messages from a channel and forwards them into the processing pipeline
/// </summary>
public interface IChannelReceiver
{
    /// <summary>
    /// Starts receiving messages
    /// </summary>
    /// <param name="stopFetching">
    /// Stops taking new messages, while the messages already being handled carry on. A receiver
    /// that cannot stop fetching on its own treats it as <paramref name="cancellationToken"/>
    /// </param>
    /// <param name="cancellationToken">Stops the receiver and cancels the messages it is handling</param>
    Task StartAsync(CancellationToken stopFetching, CancellationToken cancellationToken);

    IProcessingSettings Settings { get; set; }
}
