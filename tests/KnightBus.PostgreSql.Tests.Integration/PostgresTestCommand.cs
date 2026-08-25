using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;

namespace KnightBus.PostgreSql.Tests.Integration;

public class PostgresTestCommand : IPostgresCommand
{
    public string Value { get; set; }

    public PostgresTestCommand(string value)
    {
        Value = value;
    }
}

public class PostgresTestCommandMapping : IMessageMapping<PostgresTestCommand>
{
    public string QueueName => "test";
}
