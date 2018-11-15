using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core
{
    /// <summary>
    /// Middleware for message processing
    /// </summary>
    public interface IMessageProcessorMiddleware
    {
        Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage;
    }

    
}