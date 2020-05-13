using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

[assembly: InternalsVisibleTo("KnightBus.Host.Tests.Unit")]
namespace KnightBus.Host
{
    internal class MessageProcessor<TMessageProcessor> : IMessageProcessor
    {
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, CancellationToken cancellationToken) where T : class, IMessage
        {
                var typedMessage = await messageStateHandler.GetMessageAsync().ConfigureAwait(false);
                var messageHandler = messageStateHandler.MessageScope.GetInstance<IProcessMessage<T>>(typeof(TMessageProcessor));

                await messageHandler.ProcessAsync(typedMessage, cancellationToken).ConfigureAwait(false);
                await messageStateHandler.CompleteAsync().ConfigureAwait(false);
            }
    }
}