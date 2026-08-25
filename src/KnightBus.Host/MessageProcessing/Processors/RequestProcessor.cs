using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host.MessageProcessing.Processors;

/// <summary>
/// Responsible for resolving the user registered processor, invoking it and returning the response.
/// Used with replies.
/// </summary>
internal class RequestProcessor<TResponse> : IMessageProcessor
{
    private readonly Type _messageHandlerType;

    public RequestProcessor(Type messageHandlerType)
    {
        _messageHandlerType = messageHandlerType;
    }

    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        var typedMessage = messageStateHandler.GetMessage();
        var messageHandler = messageStateHandler.MessageScope.GetInstance<
            IProcessMessage<T, Task<TResponse>>
        >(_messageHandlerType);

        var response = await messageHandler
            .ProcessAsync(typedMessage, cancellationToken)
            .ConfigureAwait(false);
        await messageStateHandler.CompleteAsync().ConfigureAwait(false);
        await messageStateHandler.ReplyAsync(response).ConfigureAwait(false);
    }
}
