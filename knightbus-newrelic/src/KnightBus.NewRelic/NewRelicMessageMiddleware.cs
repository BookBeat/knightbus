using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DistributedTracing;
using KnightBus.Messages;
using NewRelic.Api.Agent;

namespace KnightBus.NewRelicMiddleware
{
    public class NewRelicMessageMiddleware : IMessageProcessorMiddleware
    {

        [Transaction]
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            var messageName = typeof(T).FullName;
            NewRelic.Api.Agent.NewRelic.SetTransactionName("Message", messageName);
            try
            {
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                NewRelic.Api.Agent.NewRelic.NoticeError(e);
                throw;
            }
        }
    }
}
