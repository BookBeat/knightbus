using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus;

public interface IServiceBus
{
    /// <summary>
    /// Schedules a queue message for delivery a certain time into the future and returns a sequence number for the scheduled message.
    /// </summary>
    /// <remarks>
    /// Although the message will not be available to be received until the scheduled enqueue time, it can still be peeked before that time.
    /// </remarks>
    ///
    /// <returns>The sequence number of the message that was scheduled.</returns>
    Task<long> ScheduleAsync<T>(
        T message,
        TimeSpan span,
        CancellationToken cancellationToken = default
    )
        where T : IServiceBusCommand;

    /// <summary>
    /// Schedules a batch of queue messages for delivery a certain time into the future and returns sequence numbers for the scheduled messages.
    /// </summary>
    /// <remarks>
    /// Although the messages will not be available to be received until the scheduled enqueue time, they can still be peeked before that time.
    /// The sequence numbers are returned in the same order as the input messages, enabling positional correlation.
    /// When scheduling, the result is atomic; either all messages succeed or all fail. Partial success is not possible.
    /// </remarks>
    /// <returns>
    /// A read-only list of sequence numbers for the scheduled messages, ordered to match the input messages.
    /// </returns>
    Task<IReadOnlyList<long>> ScheduleAsync<T>(
        IEnumerable<T> messages,
        TimeSpan span,
        CancellationToken cancellationToken = default
    )
        where T : IServiceBusCommand;

    /// <summary>
    /// Sends a queue message immediately
    /// </summary>
    Task SendAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : IServiceBusCommand;

    /// <summary>
    /// Sends a batch of messages using the batch send method
    /// </summary>
    Task SendAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken = default)
        where T : IServiceBusCommand;

    /// <summary>
    /// Sends a topic message immediately
    /// </summary>
    Task PublishEventAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : IServiceBusEvent;

    /// <summary>
    /// Sends a batch of topic message immediately using the batch send method
    /// </summary>
    Task PublishEventsAsync<T>(
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default
    )
        where T : IServiceBusEvent;

    /// <summary>
    /// Cancels a scheduled message using the sequence number returned when the message was scheduled.
    /// </summary>
    Task CancelScheduledAsync<T>(long sequenceNumber, CancellationToken cancellationToken = default)
        where T : IServiceBusCommand;
}

public class ServiceBus : IServiceBus
{
    private readonly IServiceBusConfiguration _configuration;
    private readonly IClientFactory _clientFactory;
    private readonly ConcurrentDictionary<Type, IMessageSerializer> _serializers;
    private readonly IEnumerable<IMessagePreProcessor> _messagePreProcessors;

    public ServiceBus(
        IServiceBusConfiguration config,
        IClientFactory clientFactory,
        IEnumerable<IMessagePreProcessor> messagePreProcessors
    )
    {
        _configuration = config;
        _clientFactory = clientFactory;
        _messagePreProcessors = messagePreProcessors;
        _serializers = new ConcurrentDictionary<Type, IMessageSerializer>();
    }

    public async Task SendAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : IServiceBusCommand
    {
        var client = await _clientFactory.GetSenderClient<T>().ConfigureAwait(false);
        var sbMessage = await CreateMessageAsync(message, cancellationToken).ConfigureAwait(false);

        await SendAsync(client, sbMessage, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendAsync<T>(
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default
    )
        where T : IServiceBusCommand
    {
        var client = await _clientFactory.GetSenderClient<T>().ConfigureAwait(false);
        var sbMessages = new Queue<ServiceBusMessage>();
        foreach (var message in messages)
        {
            sbMessages.Enqueue(
                await CreateMessageAsync(message, cancellationToken).ConfigureAwait(false)
            );
        }

        await SendAsync(client, sbMessages, cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> ScheduleAsync<T>(
        T message,
        TimeSpan span,
        CancellationToken cancellationToken = default
    )
        where T : IServiceBusCommand
    {
        var client = await _clientFactory.GetSenderClient<T>().ConfigureAwait(false);
        var sbMessage = await CreateMessageAsync(message, cancellationToken).ConfigureAwait(false);
        var scheduledEnqueueTime = DateTime.UtcNow.Add(span);

        return await client
            .ScheduleMessageAsync(sbMessage, scheduledEnqueueTime, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<long>> ScheduleAsync<T>(
        IEnumerable<T> messages,
        TimeSpan span,
        CancellationToken cancellationToken = default
    )
        where T : IServiceBusCommand
    {
        // Early exit if messages is already materialized and has no items
        if (messages is ICollection<T> { Count: 0 })
        {
            return [];
        }

        var client = await _clientFactory.GetSenderClient<T>().ConfigureAwait(false);
        var scheduledEnqueueTime = DateTime.UtcNow.Add(span);
        var sbMessages = messages is ICollection<T> c
            ? new List<ServiceBusMessage>(c.Count) // Pre-size the list for performance
            : new List<ServiceBusMessage>(); // Not materialized, use default list

        foreach (var message in messages)
        {
            var msg = await CreateMessageAsync(message, cancellationToken).ConfigureAwait(false);
            sbMessages.Add(msg);
        }

        if (sbMessages.Count == 0)
        {
            return [];
        }

        return await client
            .ScheduleMessagesAsync(sbMessages, scheduledEnqueueTime, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task PublishEventAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : IServiceBusEvent
    {
        var client = await _clientFactory.GetSenderClient<T>().ConfigureAwait(false);
        var brokeredMessage = await CreateMessageAsync(message, cancellationToken)
            .ConfigureAwait(false);

        await SendAsync(client, brokeredMessage, cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishEventsAsync<T>(
        IEnumerable<T> messages,
        CancellationToken cancellationToken = default
    )
        where T : IServiceBusEvent
    {
        var client = await _clientFactory.GetSenderClient<T>().ConfigureAwait(false);
        var sbMessages = new Queue<ServiceBusMessage>();
        foreach (var message in messages)
        {
            sbMessages.Enqueue(
                await CreateMessageAsync(message, cancellationToken).ConfigureAwait(false)
            );
        }

        await SendAsync(client, sbMessages, cancellationToken).ConfigureAwait(false);
    }

    public async Task CancelScheduledAsync<T>(
        long sequenceNumber,
        CancellationToken cancellationToken = default
    )
        where T : IServiceBusCommand
    {
        var client = await _clientFactory.GetSenderClient<T>().ConfigureAwait(false);

        await client
            .CancelScheduledMessageAsync(sequenceNumber, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SendAsync(
        ServiceBusSender client,
        ServiceBusMessage message,
        CancellationToken cancellationToken
    )
    {
        await client.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendAsync(
        ServiceBusSender client,
        Queue<ServiceBusMessage> messages,
        CancellationToken cancellationToken
    )
    {
        while (messages.Count > 0)
        {
            using var messageBatch = await client
                .CreateMessageBatchAsync(cancellationToken)
                .ConfigureAwait(false);

            if (messageBatch.TryAddMessage(messages.Peek()))
            {
                messages.Dequeue();
            }
            else
            {
                // First message too large. Won't be able to send it so better throw exception
                throw new ServiceBusMessageTooLargeException();
            }

            // Add as many messages as possible to the current batch
            while (messages.Count > 0 && messageBatch.TryAddMessage(messages.Peek()))
            {
                messages.Dequeue();
            }

            await client.SendMessagesAsync(messageBatch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ServiceBusMessage> CreateMessageAsync<T>(
        T body,
        CancellationToken cancellationToken
    )
        where T : IMessage
    {
        var serializer = GetSerializer<T>();
        var message = new ServiceBusMessage(serializer.Serialize(body))
        {
            ContentType = serializer.ContentType,
        };

        foreach (var preProcessor in _messagePreProcessors)
        {
            var properties = await preProcessor.PreProcess(body, cancellationToken);
            foreach (var property in properties)
            {
                message.ApplicationProperties[property.Key] = property.Value;
            }
        }

        return message;
    }

    private IMessageSerializer GetSerializer<T>()
        where T : IMessage
    {
        return _serializers.GetOrAdd(
            typeof(T),
            type =>
            {
                var mapper = AutoMessageMapper.GetMapping<T>();
                if (mapper is ICustomMessageSerializer serializer)
                    return serializer.MessageSerializer;
                return _configuration.MessageSerializer;
            }
        );
    }
}
