using Microsoft.WindowsAzure.Storage;

namespace KnightBus.Azure.Storage.Singleton
{
    internal static class StorageExceptionExtensions
    {
        public static bool IsServerSideError(this StorageException exception)
        {
            var statusCode = exception.RequestInformation?.HttpStatusCode;
            if (!statusCode.HasValue) return false;
            return statusCode >= 500 && statusCode < 600;
        }
    }
}