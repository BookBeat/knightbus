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
        private readonly IAgent _agent;

        public NewRelicMessageMiddleware()
        {
            _agent = NewRelic.Api.Agent.NewRelic.GetAgent();
        }

        [Transaction]
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            var messageName = typeof(T).FullName;
            NewRelic.Api.Agent.NewRelic.SetTransactionName("Message", messageName);
            var transaction = _agent.CurrentTransaction;
            try
            {
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);

                var tracingProvider = messageStateHandler.MessageScope.GetInstance<IDistributedTracingProvider>();
                tracingProvider.SetProperties(messageStateHandler.MessageProperties);

                // Add the message properties to the telemetry log
                foreach (var property in messageStateHandler.MessageProperties)
                    transaction.AddCustomAttribute(property.Key, property.Value);
            }
            catch (Exception e)
            {
                NewRelic.Api.Agent.NewRelic.NoticeError(e);
                throw;
            }
        }
    }
}
