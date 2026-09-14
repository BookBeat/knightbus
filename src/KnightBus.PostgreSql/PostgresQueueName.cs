using System.Diagnostics.CodeAnalysis;

namespace KnightBus.PostgreSql;

/// <summary>
/// A queue, topic or subscription name that is safe to interpolate into a Postgres identifier.
/// Names are restricted to letters, digits and '_', so they can never terminate the identifier
/// or introduce additional statements.
/// </summary>
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
                "Postgres queue names must not be empty and can only contain letters, digits and '_'. Prefer '_' over '-'. https://www.postgresql.org/docs/current/sql-syntax-lexical.html#SQL-SYNTAX-IDENTIFIERS",
                nameof(input)
            )
            : new PostgresQueueName(input);

    public static bool TryCreate(string? input, out PostgresQueueName name)
    {
        if (input is null || !AssertInput(input))
        {
            name = default;
            return false;
        }

        name = new PostgresQueueName(input);
        return true;
    }

    private static bool AssertInput([NotNullWhen(true)] string? input) =>
        !string.IsNullOrEmpty(input) && input.All(c => char.IsLetterOrDigit(c) || c == '_');

    public override string ToString() => Value;
}
