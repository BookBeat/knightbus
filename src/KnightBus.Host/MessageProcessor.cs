using System.Reflection;
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
        private readonly IMessageProcessorProvider _processorProvider;

        public MessageProcessor(IMessageProcessorProvider processorProvider)
        {
            _processorProvider = processorProvider;
        }
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, CancellationToken cancellationToken) where T : class, IMessage
        {
            var typedMessage = await messageStateHandler.GetMessageAsync().ConfigureAwait(false);
            var messageHandler = _processorProvider.GetProcessor<T>(typeof(TMessageProcessor));

            await messageHandler.ProcessAsync(typedMessage, cancellationToken)
                .ContinueWith(task => messageStateHandler.CompleteAsync(), TaskContinuationOptions.OnlyOnRanToCompletion)
                .ConfigureAwait(false);
        }
    }
}