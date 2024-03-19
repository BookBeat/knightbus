using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core;

/// <summary>
/// Message processing
/// </summary>
public interface IMessageProcessor
{
    Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, CancellationToken cancellationToken) where T : class, IMessage;
}
