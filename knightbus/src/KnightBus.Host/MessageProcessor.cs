using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

[assembly: InternalsVisibleTo("KnightBus.Host.Tests.Unit")]
namespace KnightBus.Host
{
    /// <summary>
    /// Responsible for resolving the user registered processor, invoking it and completing the message.
    /// Used without replies.
    /// </summary>
    internal class MessageProcessor : IMessageProcessor
    {
        private readonly Type _messageHandlerType;

        public MessageProcessor(Type messageHandlerType)
        {
            _messageHandlerType = messageHandlerType;
        }
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, CancellationToken cancellationToken) where T : class, IMessage
        {
            var typedMessage = await messageStateHandler.GetMessageAsync().ConfigureAwait(false);
            var messageHandler = messageStateHandler.MessageScope.GetInstance<IProcessMessage<T>>(_messageHandlerType);

            await messageHandler.ProcessAsync(typedMessage, cancellationToken).ConfigureAwait(false);
            await messageStateHandler.CompleteAsync().ConfigureAwait(false);
        }   
    }
    
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
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, CancellationToken cancellationToken) where T : class, IMessage
        {
            var typedMessage = await messageStateHandler.GetMessageAsync().ConfigureAwait(false);
            var messageHandler = messageStateHandler.MessageScope.GetInstance<IProcessRequest<T,TResponse>>(_messageHandlerType);

            var response = await messageHandler.ProcessAsync(typedMessage, cancellationToken).ConfigureAwait(false);
            await messageStateHandler.CompleteAsync().ConfigureAwait(false);
            await messageStateHandler.ReplyAsync(response).ConfigureAwait(false);
        }   
    }
}