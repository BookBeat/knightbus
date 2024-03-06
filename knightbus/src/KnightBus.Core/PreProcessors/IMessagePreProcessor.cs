using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.PreProcessors;

public interface IMessagePreProcessor
{
    /// <summary>
    /// Runs before a message is sent for preprocessing of a message.
    /// </summary>
    /// <returns>A dictionary containing properties to be sent with the message. In most cases this will be string, string.</returns>
    Task<IDictionary<string, object>> PreProcess<T>(T message, CancellationToken cancellationToken) where T : IMessage;
}
