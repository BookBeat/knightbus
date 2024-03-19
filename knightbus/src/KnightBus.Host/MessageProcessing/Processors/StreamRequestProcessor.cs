using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host.MessageProcessing.Processors;

internal class StreamRequestProcessor<TResponse> : IMessageProcessor
{
    private readonly Type _messageHandlerType;

    public StreamRequestProcessor(Type messageHandlerType)
    {
        _messageHandlerType = messageHandlerType;
    }
    public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, CancellationToken cancellationToken) where T : class, IMessage
    {
        var typedMessage = messageStateHandler.GetMessage();
        var messageHandler = messageStateHandler.MessageScope.GetInstance<IProcessMessage<T, IAsyncEnumerable<TResponse>>>(_messageHandlerType);

        await foreach (var response in messageHandler.ProcessAsync(typedMessage, cancellationToken))
        {
            await messageStateHandler.ReplyAsync(response).ConfigureAwait(false);
        }
        await messageStateHandler.CompleteAsync().ConfigureAwait(false);
    }
}
