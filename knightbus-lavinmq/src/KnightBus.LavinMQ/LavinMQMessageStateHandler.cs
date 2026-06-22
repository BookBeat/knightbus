using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ;

public class LavinMQMessageStateHandler<T> : IMessageStateHandler<T>
    where T : class, IMessage
{
    private readonly IChannel _channel;
    private readonly ulong _deliveryTag;
    private readonly IReadOnlyBasicProperties _properties;
    private readonly T _message;

    public LavinMQMessageStateHandler(
        IChannel channel,
        ulong deliveryTag,
        bool redelivered,
        ReadOnlyMemory<byte> body,
        IReadOnlyBasicProperties properties,
        IMessageSerializer serializer,
        int deadLetterDeliveryLimit,
        IDependencyInjection messageScope
    )
    {
        _channel = channel;
        _deliveryTag = deliveryTag;
        _properties = properties;
        DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
        MessageScope = messageScope;
        DeliveryCount = ResolveDeliveryCount(properties, redelivered);
        _message = serializer.Deserialize<T>(body.Span);
    }

    public int DeliveryCount { get; }
    public int DeadLetterDeliveryLimit { get; }

    public IDictionary<string, string> MessageProperties => ReadHeaders(_properties);

    public Task CompleteAsync() => _channel.BasicAckAsync(_deliveryTag, multiple: false).AsTask();

    public Task AbandonByErrorAsync(Exception e) =>
        _channel.BasicNackAsync(_deliveryTag, multiple: false, requeue: true).AsTask();

    public Task DeadLetterAsync(int deadLetterLimit) =>
        _channel.BasicRejectAsync(_deliveryTag, requeue: false).AsTask();

    // Request/reply is not supported by the LavinMQ transport yet. Fail loudly rather than silently
    // dropping the reply and leaving the caller to block until timeout.
    public Task ReplyAsync<TReply>(TReply reply) =>
        throw new NotSupportedException("Request/reply is not supported by the LavinMQ transport.");

    public T GetMessage() => _message;

    public IDependencyInjection MessageScope { get; set; }

    /// <summary>
    /// LavinMQ enforces the hard delivery limit server-side via <c>x-delivery-limit</c>. When it exposes a
    /// redelivery count header we surface it (so KnightBus' dead-letter middleware and the
    /// IProcessBeforeDeadLetter hook fire), otherwise we fall back to the AMQP redelivered flag.
    /// </summary>
    private static int ResolveDeliveryCount(IReadOnlyBasicProperties properties, bool redelivered)
    {
        if (
            properties?.Headers != null
            && properties.Headers.TryGetValue(
                LavinMQQueueConventions.DeliveryCountHeader,
                out var raw
            )
            && raw != null
        )
        {
            try
            {
                return Convert.ToInt32(raw) + 1;
            }
            catch (Exception)
            {
                // Fall through to the redelivered heuristic
            }
        }

        return redelivered ? 2 : 1;
    }

    private static IDictionary<string, string> ReadHeaders(IReadOnlyBasicProperties properties)
    {
        var headers = new Dictionary<string, string>();
        if (properties.Headers == null)
            return headers;

        foreach (var header in properties.Headers)
        {
            headers[header.Key] = header.Value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                null => string.Empty,
                _ => header.Value.ToString() ?? string.Empty,
            };
        }

        return headers;
    }
}
