using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;
using NewRelic.Api.Agent;

[assembly: InternalsVisibleTo("KnightBus.NewRelic.Tests.Unit")]

namespace KnightBus.NewRelicMiddleware;

public class NewRelicMessageMiddleware : IMessageProcessorMiddleware
{
    internal const string QueueAttribute = "knightbus.queue";
    internal const string TopicAttribute = "knightbus.topic";
    internal const string SubscriptionAttribute = "knightbus.subscription";

    [Transaction]
    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        var messageName = typeof(T).FullName!;
        NewRelic.Api.Agent.NewRelic.SetTransactionName("Message", messageName);
        AddCustomAttributes(typeof(T), pipelineInformation);
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

    /// <summary>
    /// Tags the current transaction with the queue (commands) or topic and subscription (events)
    /// the message came from. Telemetry must never break message processing, so nothing here throws.
    /// </summary>
    private static void AddCustomAttributes(
        Type messageType,
        IPipelineInformation pipelineInformation
    )
    {
        try
        {
            var transaction = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;
            foreach (var (key, value) in GetCustomAttributes(messageType, pipelineInformation))
            {
                transaction.AddCustomAttribute(key, value);
            }
        }
        catch
        {
            // Never let telemetry interfere with the pipeline
        }
    }

    /// <summary>
    /// Computes the custom attributes for a message without touching the New Relic agent.
    /// A type without a mapping yields no queue or topic attribute.
    /// </summary>
    internal static IReadOnlyList<KeyValuePair<string, string>> GetCustomAttributes(
        Type messageType,
        IPipelineInformation pipelineInformation
    )
    {
        var attributes = new List<KeyValuePair<string, string>>(2);
        var subscription = pipelineInformation.Subscription;
        var queueName = TryGetQueueName(messageType);

        if (subscription is null)
        {
            if (queueName is not null)
                attributes.Add(new KeyValuePair<string, string>(QueueAttribute, queueName));
        }
        else
        {
            // An event's mapping names its topic; the subscription is what the processor listens on
            if (queueName is not null)
                attributes.Add(new KeyValuePair<string, string>(TopicAttribute, queueName));
            attributes.Add(
                new KeyValuePair<string, string>(SubscriptionAttribute, subscription.Name)
            );
        }

        return attributes;
    }

    private static string? TryGetQueueName(Type messageType)
    {
        try
        {
            return AutoMessageMapper.GetQueueName(messageType);
        }
        catch (MessageMappingMissingException)
        {
            return null;
        }
    }
}
