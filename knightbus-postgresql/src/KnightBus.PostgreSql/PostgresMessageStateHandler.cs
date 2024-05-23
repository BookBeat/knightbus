using KnightBus.Core;
using KnightBus.Messages;
using Npgsql;

namespace KnightBus.PostgreSql;

public class PostgresMessageStateHandler<T> :
    IMessageStateHandler<T> where T : class, IMessage
{
    private readonly PostgresQueueClient<T> _queueClient;
    private readonly PostgresMessage<T> _message;

    public PostgresMessageStateHandler(
        NpgsqlDataSource npgsqlDataSource,
        PostgresMessage<T> message,
        int deadLetterDeliveryLimit,
        IMessageSerializer serializer,
        IDependencyInjection messageScope)
    {
        _queueClient = new PostgresQueueClient<T>(npgsqlDataSource, serializer);
        _message = message;
        DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
        MessageScope = messageScope;
    }

    public int DeliveryCount => _message.ReadCount;
    public int DeadLetterDeliveryLimit { get; }
    public IDictionary<string, string> MessageProperties => _message.Properties;

    public Task CompleteAsync()
    {
        return _queueClient.CompleteAsync(_message);
    }

    public Task AbandonByErrorAsync(Exception e)
    {
        return _queueClient.AbandonByErrorAsync(_message, e);
    }

    public Task DeadLetterAsync(int deadLetterLimit)
    {
        return _queueClient.DeadLetterMessageAsync(_message);
    }

    public T GetMessage()
    {
        return _message.Message;
    }

    public Task ReplyAsync<TReply>(TReply reply)
    {
        throw new NotImplementedException();
    }

    public IDependencyInjection MessageScope { get; set; }
}
