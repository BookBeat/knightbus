using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Microsoft.DependencyInjection
{
    public class MicrosoftDependencyInjectionScopedLifeStyleMiddleware : IMessageProcessorMiddleware
    {
        private readonly IServiceProvider _serviceProvider;

        public MicrosoftDependencyInjectionScopedLifeStyleMiddleware(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            using (_serviceProvider.CreateScope())
            {
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}