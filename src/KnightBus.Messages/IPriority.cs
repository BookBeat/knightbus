namespace KnightBus.Messages;

/// <summary>
/// Marks a message as having a processing priority. Higher values are processed first.
/// Only honored by transports that support priority queueing (currently PostgreSql).
/// </summary>
public interface IPriority
{
    int Priority { get; set; }
}
