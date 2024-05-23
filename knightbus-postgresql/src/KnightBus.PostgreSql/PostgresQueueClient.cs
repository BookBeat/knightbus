using KnightBus.Core;
using KnightBus.Messages;
using Npgsql;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public class PostgresQueueClient<T> : PostgresBaseClient<T> where T : class, IMessage
{
    public PostgresQueueClient(NpgsqlDataSource npgsqlDataSource, IMessageSerializer serializer)
    :base(npgsqlDataSource, serializer, QueuePrefix, PostgresQueueName.Create(AutoMessageMapper.GetQueueName<T>()))
    { }
}

public class PostgresSubscriptionClient<T> : PostgresBaseClient<T> where T : class, IMessage
{
    public PostgresSubscriptionClient(NpgsqlDataSource npgsqlDataSource, IMessageSerializer serializer)
        :base(npgsqlDataSource, serializer, SubscriptionPrefix, PostgresQueueName.Create(AutoMessageMapper.GetQueueName<T>()))
    { }
}
