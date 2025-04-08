namespace KnightBus.PostgreSql;

public readonly struct PostgresQueueName
{
    public string Value { get; }

    private PostgresQueueName(string value)
    {
        Value = value;
    }

    public static PostgresQueueName Create(string input) =>
        !AssertInput(input)
            ? throw new ArgumentException(
                "Postgres queue names can not contain '-'. Prefer using '_'. https://www.postgresql.org/docs/7.0/syntax525.htm",
                nameof(input)
            )
            : new PostgresQueueName(input);

    private static bool AssertInput(string input) =>
        input.All(c => char.IsLetterOrDigit(c) || c == '_');

    public override string ToString() => Value;
}
