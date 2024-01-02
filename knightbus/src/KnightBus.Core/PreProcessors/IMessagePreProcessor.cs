using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.PreProcessors;

public interface IMessagePreProcessor
{
    Task Process<T>(T message, Action<string, string> setter, CancellationToken cancellationToken) where T : IMessage;
}
