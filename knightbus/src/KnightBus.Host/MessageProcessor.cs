using System;
using System.Collections.Generic;
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
            var messageHandler = messageStateHandler.MessageScope.GetInstance<IProcessMessage<T, Task>>(_messageHandlerType);

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
            var messageHandler = messageStateHandler.MessageScope.GetInstance<IProcessMessage<T,Task<TResponse>>>(_messageHandlerType);

            var response = await messageHandler.ProcessAsync(typedMessage, cancellationToken).ConfigureAwait(false);
            await messageStateHandler.CompleteAsync().ConfigureAwait(false);
            await messageStateHandler.ReplyAsync(response).ConfigureAwait(false);
        }   
    }

    internal class StreamRequestProcessor<TResponse> : IMessageProcessor
    {
        private readonly Type _messageHandlerType;

        public StreamRequestProcessor(Type messageHandlerType)
        {
            _messageHandlerType = messageHandlerType;
        }
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, CancellationToken cancellationToken) where T : class, IMessage
        {
            var typedMessage = await messageStateHandler.GetMessageAsync().ConfigureAwait(false);
            var messageHandler = messageStateHandler.MessageScope.GetInstance<IProcessMessage<T, IAsyncEnumerable<TResponse>>>(_messageHandlerType);

            await foreach (var response in messageHandler.ProcessAsync(typedMessage, cancellationToken))
            {
                await messageStateHandler.ReplyAsync(response).ConfigureAwait(false);
            }
            await messageStateHandler.CompleteAsync().ConfigureAwait(false);
        }
    }
}