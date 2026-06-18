using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.LavinMQ.Messages;
using KnightBus.Messages;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ;

public interface ILavinMQBus
{
    Task SendAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : ILavinMQCommand;
    Task SendAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken = default)
        where T : ILavinMQCommand;
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : ILavinMQEvent;
    Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken = default)
        where T : ILavinMQEvent;
    Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken cancellationToken = default)
        where T : ILavinMQCommand;
    Task ScheduleAsync<T>(
        IEnumerable<T> messages,
        TimeSpan delay,
        CancellationToken cancellationToken = default
    )
        where T : ILavinMQCommand;
}

public class LavinMQBus : ILavinMQBus
{
    private readonly IConnection _connection;
    private readonly ILavinMQConfiguration _configuration;
    private readonly IEnumerable<IMessagePreProcessor> _messagePreProcessors;

    public LavinMQBus(
        IConnection connection,
        ILavinMQConfiguration configuration,
        IEnumerable<IMessagePreProcessor> messagePreProcessors
    )
    {
        _connection = connection;
        _configuration = configuration;
        _messagePreProcessors = messagePreProcessors;
    }

    public Task SendAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : ILavinMQCommand => SendAsync(new[] { message }, cancellationToken);

    public async Task SendAsync<T>(
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default
    )
        where T : ILavinMQCommand
    {
        await using var channel = await _connection
            .CreateChannelAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        foreach (var message in messages)
        {
            // Commands are published to the default exchange ("") keyed by the queue name.
            await PublishAsync(
                    channel,
                    exchange: string.Empty,
                    useQueueNameAsRoutingKey: true,
                    message,
                    delay: null,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : ILavinMQEvent => PublishAsync(new[] { message }, cancellationToken);

    public async Task PublishAsync<T>(
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default
    )
        where T : ILavinMQEvent
    {
        await using var channel = await _connection
            .CreateChannelAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        foreach (var message in messages)
        {
            var topic = AutoMessageMapper.GetQueueName(message.GetType());
            await LavinMQTopology
                .DeclareEventExchangeAsync(channel, topic, cancellationToken)
                .ConfigureAwait(false);
            // Events fan out to all bound subscription queues; the routing key is ignored.
            await PublishAsync(
                    channel,
                    exchange: topic,
                    useQueueNameAsRoutingKey: false,
                    message,
                    delay: null,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    public Task ScheduleAsync<T>(
        T message,
        TimeSpan delay,
        CancellationToken cancellationToken = default
    )
        where T : ILavinMQCommand => ScheduleAsync(new[] { message }, delay, cancellationToken);

    public async Task ScheduleAsync<T>(
        IEnumerable<T> messages,
        TimeSpan delay,
        CancellationToken cancellationToken = default
    )
        where T : ILavinMQCommand
    {
        await using var channel = await _connection
            .CreateChannelAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await LavinMQTopology
            .DeclareDelayedExchangeAsync(channel, cancellationToken)
            .ConfigureAwait(false);
        foreach (var message in messages)
        {
            // The delayed exchange holds the message for the delay, then routes it (direct) to the queue.
            await PublishAsync(
                    channel,
                    exchange: LavinMQQueueConventions.DelayedExchangeName,
                    useQueueNameAsRoutingKey: true,
                    message,
                    delay,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    private async Task PublishAsync(
        IChannel channel,
        string exchange,
        bool useQueueNameAsRoutingKey,
        IMessage message,
        TimeSpan? delay,
        CancellationToken cancellationToken
    )
    {
        var mapping = AutoMessageMapper.GetMapping(message.GetType());
        var serializer = _configuration.MessageSerializer;
        if (mapping is ICustomMessageSerializer customSerializer)
            serializer = customSerializer.MessageSerializer;

        var headers = new Dictionary<string, object?>();
        foreach (var preProcessor in _messagePreProcessors)
        {
            var properties = await preProcessor
                .PreProcess(message, cancellationToken)
                .ConfigureAwait(false);
            foreach (var property in properties)
                headers[property.Key] = property.Value;
        }

        if (delay.HasValue)
            headers[LavinMQQueueConventions.DelayHeader] = (long)delay.Value.TotalMilliseconds;

        var basicProperties = new BasicProperties
        {
            Persistent = true,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };
        if (headers.Count > 0)
            basicProperties.Headers = headers;

        var routingKey = useQueueNameAsRoutingKey ? mapping.QueueName : string.Empty;
        await channel
            .BasicPublishAsync(
                exchange,
                routingKey,
                mandatory: false,
                basicProperties: basicProperties,
                body: serializer.Serialize(message),
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
    }
}
