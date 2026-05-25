using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.PreProcessors;

public interface IMessagePreProcessor
{
    /// <summary>
    /// Runs before a message is sent for preprocessing of a message.
    /// </summary>
    /// <returns>A result containing optional properties to attach to the message, or an abort signal to cancel the send.</returns>
    Task<MessagePreProcessorResult> PreProcess<T>(T message, CancellationToken cancellationToken)
        where T : IMessage;
}
