using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using KnightBus.Core.Singleton;

namespace KnightBus.Azure.Storage.Singleton
{
    internal class BlobLockManager : ISingletonLockManager
    {
        private readonly string _connectionString;
        private BlobContainerClient _client;
        private readonly IBlobLockScheme _lockScheme;

        public BlobLockManager(string connectionString, IBlobLockScheme lockScheme = null)
        {
            _connectionString = connectionString;
            _lockScheme =  lockScheme ?? new DefaultBlobLockScheme();
        }

        public BlobLockManager(IStorageBusConfiguration configuration, IBlobLockScheme lockScheme = null) : this(configuration.ConnectionString, lockScheme)
        {
        }

        public Task InitializeAsync()
        {
            if (_client == null)
            {
                _client = new BlobContainerClient(_connectionString, _lockScheme.ContainerName);
            }

            return Task.CompletedTask;
        }

        public async Task<ISingletonLockHandle> TryLockAsync(string lockId, TimeSpan lockPeriod, CancellationToken cancellationToken)
        {
            var blob = _client.GetBlobClient(Path.Combine(_lockScheme.Directory, lockId));

            var lease = await TryAcquireLeaseAsync(blob, lockPeriod, cancellationToken).ConfigureAwait(false);

            if (lease == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(lease.LeaseId))
            {
                await WriteLeaseBlobMetadata(blob, lease.LeaseId, lockId, cancellationToken).ConfigureAwait(false);
            }

            var lockHandle = new BlobLockHandle(lease.LeaseId, lockId, blob.GetBlobLeaseClient(lease.LeaseId), lockPeriod);

            return lockHandle;
        }

        private static async Task WriteLeaseBlobMetadata(BlobBaseClient blob, string leaseId, string functionInstanceId,
            CancellationToken cancellationToken)
        {
            await blob.SetMetadataAsync(new Dictionary<string, string> {{"FunctionInstance", functionInstanceId}}, new BlobRequestConditions
            {
                LeaseId = leaseId
            }, cancellationToken);
        }

        private static async Task<BlobLease> TryAcquireLeaseAsync(
            BlobClient blob,
            TimeSpan leasePeriod,
            CancellationToken cancellationToken)
        {
            var leaseClient = blob.GetBlobLeaseClient();
            try
            {
                // Optimistically try to acquire the lease. The blob may not yet
                // exist. If it doesn't we handle the 404, create it, and retry below
                return await leaseClient.AcquireAsync(leasePeriod, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException exception)
            {
                switch (exception.Status)
                {
                    case 409:
                        return null;
                    case 404:
                        break;
                    default:
                        throw;
                }
            }

            //404
            await TryCreateAsync(blob, cancellationToken).ConfigureAwait(false);

            try
            {
                return await leaseClient.AcquireAsync(leasePeriod, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 409)
                {
                    return null;
                }

                throw;
            }
        }

        private static async Task<bool> TryCreateAsync(BlobClient blob, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(string.Empty);
                using (var stream = new MemoryStream(bytes))
                {
                    await blob.UploadAsync(stream, cancellationToken).ConfigureAwait(false);
                }

                return true;
            }
            catch (RequestFailedException exception)
            {
                switch (exception.Status)
                {
                    case 404:
                        break;
                    case 409:
                    case 412:
                        // The blob already exists, or is leased by someone else
                        return false;
                    default:
                        throw;
                }
            }

            var container = blob.GetParentBlobContainerClient();
            try
            {
                await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException exc)
                when (exc.Status == 409 && exc.ErrorCode == BlobErrorCode.ContainerBeingDeleted)
            {
                throw;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(string.Empty);
                using (var stream = new MemoryStream(bytes))
                {
                    await blob.UploadAsync(stream, cancellationToken).ConfigureAwait(false);
                }

                return true;
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 409 || exception.Status == 412)
                {
                    // The blob already exists, or is leased by someone else
                    return false;
                }

                throw;
            }
        }
    }
}