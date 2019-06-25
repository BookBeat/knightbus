using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core
{
    /// <summary>
    /// Mark a class to handle final message failures and a hook into when the message will be dead lettered
    /// </summary>
    public interface IProcessDeadletter<T> where T : IMessage
    {
        /// <summary>
        /// Method is called right before message will be dead lettered, providing a mechanism to hook into the final failure of a retried message
        /// </summary>
        Task BeforeDeadLetterAsync(T message, CancellationToken cancellationToken);
    }
}