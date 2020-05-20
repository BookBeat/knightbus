using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Microsoft.DependencyInjection
{
    public class MicrosoftDependencyInjectionScopedLifeStyleMiddleware : IMessageScopeProviderMiddleware
    {
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            using (var scope = messageStateHandler.MessageScope.GetScope())
            {
                messageStateHandler.MessageScope = scope;
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}