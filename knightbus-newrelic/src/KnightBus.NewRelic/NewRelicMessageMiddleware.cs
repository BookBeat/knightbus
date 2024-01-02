using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
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
            transaction.AcceptDistributedTraceHeaders(messageStateHandler.MessageProperties,
                ((carrier, key) => carrier.TryGetValue(key, out var value) ? new[] { value } : null),
                TransportType.AMQP);
            try
            {
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);

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
