using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.Singleton;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace KnightBus.Azure.Storage.Singleton
{
    internal class BlobLockManager : ISingletonLockManager
    {
        private readonly string _connectionString;
        private CloudBlobClient _client;
        private readonly IBlobLockScheme _lockScheme;

        public BlobLockManager(string connectionString)
        {
            _connectionString = connectionString;
            _lockScheme = new DefaultBlobLockScheme();
        }
        public BlobLockManager(string connectionString, IBlobLockScheme lockScheme)
        {
            _connectionString = connectionString;
            _lockScheme = lockScheme;
        }

        public Task InitializeAsync()
        {
            if (_client == null)
            {
                var storage = CloudStorageAccount.Parse(_connectionString);
                _client = storage.CreateCloudBlobClient();
            }
            return Task.CompletedTask;
        }

        public async Task<ISingletonLockHandle> TryLockAsync(string lockId, TimeSpan lockPeriod, CancellationToken cancellationToken)
        {
            var container = _client.GetContainerReference(_lockScheme.ContainerName);
            var directory = container.GetDirectoryReference(_lockScheme.Directory);
            var blob = directory.GetBlockBlobReference(lockId);

            var leaseId = await TryAcquireLeaseAsync(blob, lockPeriod, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(leaseId))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(lockId))
            {
                await WriteLeaseBlobMetadata(blob, leaseId, lockId, _lockScheme.InstanceMetadataKey, cancellationToken).ConfigureAwait(false);
            }

            var lockHandle = new BlobLockHandle(leaseId, lockId, blob, lockPeriod);

            return lockHandle;
        }
        private static async Task WriteLeaseBlobMetadata(CloudBlockBlob blob, string leaseId, string functionInstanceId, string functionInstanceMetadataKey, CancellationToken cancellationToken)
        {
            blob.Metadata.Add(functionInstanceMetadataKey, functionInstanceId);

            await blob.SetMetadataAsync(
                accessCondition: new AccessCondition { LeaseId = leaseId },
                options: null,
                operationContext: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> TryAcquireLeaseAsync(
            CloudBlockBlob blob,
            TimeSpan leasePeriod,
            CancellationToken cancellationToken)
        {
            try
            {
                // Optimistically try to acquire the lease. The blob may not yet
                // exist. If it doesn't we handle the 404, create it, and retry below
                return await blob.AcquireLeaseAsync(leasePeriod).ConfigureAwait(false);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null)
                {
                    switch (exception.RequestInformation.HttpStatusCode)
                    {
                        case 409:
                            return null;
                        case 404:
                            break;
                        default:
                            throw;
                    }
                }
                else
                {
                    throw;
                }
            }

            //404
            await TryCreateAsync(blob, cancellationToken).ConfigureAwait(false);

            try
            {
                return await blob.AcquireLeaseAsync(leasePeriod).ConfigureAwait(false);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null &&
                    exception.RequestInformation.HttpStatusCode == 409)
                {
                    return null;
                }

                throw;
            }
        }

        private static async Task<bool> TryCreateAsync(CloudBlockBlob blob, CancellationToken cancellationToken)
        {
            try
            {
                await blob.UploadTextAsync(string.Empty).ConfigureAwait(false);
                return true;
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null)
                {
                    if (exception.RequestInformation.HttpStatusCode == 404)
                    {
                    }
                    else if (exception.RequestInformation.HttpStatusCode == 409 ||
                             exception.RequestInformation.HttpStatusCode == 412)
                    {
                        // The blob already exists, or is leased by someone else
                        return false;
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }

            var container = blob.Container;
            try
            {
                await container.CreateIfNotExistsAsync().ConfigureAwait(false);
            }
            catch (StorageException exc)
            when (exc.RequestInformation.HttpStatusCode == 409 && string.Compare("ContainerBeingDeleted", exc.RequestInformation.ExtendedErrorInformation?.ErrorCode) == 0)
            {
                throw new StorageException("The host container is pending deletion and currently inaccessible.");
            }

            try
            {
                await blob.UploadTextAsync(string.Empty).ConfigureAwait(false);
                return true;
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null &&
                    (exception.RequestInformation.HttpStatusCode == 409 || exception.RequestInformation.HttpStatusCode == 412))
                {
                    // The blob already exists, or is leased by someone else
                    return false;
                }

                throw;
            }
        }
    }
}
