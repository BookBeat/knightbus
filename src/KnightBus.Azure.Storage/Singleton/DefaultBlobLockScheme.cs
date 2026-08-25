namespace KnightBus.Azure.Storage.Singleton;

public interface IBlobLockScheme
{
    string ContainerName { get; }
    string Directory { get; }
}

internal class DefaultBlobLockScheme : IBlobLockScheme
{
    public string ContainerName { get; } = "knight-data";
    public string Directory { get; } = "locks";
}
