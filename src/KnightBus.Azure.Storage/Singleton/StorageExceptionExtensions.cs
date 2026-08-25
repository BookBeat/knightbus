using Azure;

namespace KnightBus.Azure.Storage.Singleton;

internal static class StorageExceptionExtensions
{
    public static bool IsServerSideError(this RequestFailedException exception)
    {
        return exception.Status >= 500 && exception.Status < 600;
    }
}
