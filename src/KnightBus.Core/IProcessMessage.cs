using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core
{
    /// <summary>
    /// Base interface shared for message processing regardless of message type
    /// </summary>
    public interface IProcessMessage<T> where T : IMessage
    {
        Task ProcessAsync(T message, CancellationToken cancellationToken);
    }
}