using System.Threading;
using KnightBus.Messages;

namespace KnightBus.Core
{
    /// <summary>
    /// Base interface shared for all processing
    /// </summary>
    public interface IGenericProcessor
    {
    }

    /// <summary>
    /// Base interface shared for message processing regardless of message type
    /// </summary>
    public interface IProcessMessage<T, TResult> : IGenericProcessor where T : IMessage
    {
        TResult ProcessAsync(T message, CancellationToken cancellationToken);
    }
}
