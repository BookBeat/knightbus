using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;

namespace KnightBus.PostgreSql.Tests.Integration;

public class PostgresTestEvent : IPostgresEvent
{
    public string Value { get; set; }

    public PostgresTestEvent(string value)
    {
        Value = value;
    }
}

public class PostgresTestEventMapping : IMessageMapping<PostgresTestEvent>
{
    public string QueueName => "test_topic";
}

public class PostgresTestEventSubscription : IEventSubscription<PostgresTestEvent>
{
    public string Name => "sub";
}
