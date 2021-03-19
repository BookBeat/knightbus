using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using KnightBus.Core;
using KnightBus.Core.Singleton;

namespace KnightBus.Azure.Storage.Singleton
{
    internal class BlobLockHandle : ISingletonLockHandle
    {
        private readonly TimeSpan _leasePeriod;

        private DateTimeOffset _lastRenewal;
        private TimeSpan _lastRenewalLatency;

        public BlobLockHandle(string leaseId, string lockId, BlobLeaseClient blob, TimeSpan leasePeriod)
        {
            LeaseId = leaseId;
            LockId = lockId;
            _leasePeriod = leasePeriod;
            Blob = blob;
        }

        public string LeaseId { get; }
        public string LockId { get; }
        private BlobLeaseClient Blob { get; }

        public async Task<bool> RenewAsync(ILog log, CancellationToken cancellationToken)
        {
            try
            {
                var condition = new BlobRequestConditions
                {
                    LeaseId = LeaseId
                };
                var requestStart = DateTimeOffset.UtcNow;
                await Blob.RenewAsync(condition, cancellationToken).ConfigureAwait(false);
                _lastRenewal = DateTime.UtcNow;
                _lastRenewalLatency = _lastRenewal - requestStart;

                // The next execution should occur after a normal delay.
                return true;
            }
            catch (RequestFailedException exception)
            {
                if (exception.IsServerSideError())
                {
                    log.Warning(exception, string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed for blob '{0}'", LockId));
                    return false; // The next execution should occur more quickly (try to renew the lease before it expires).
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    //The service using the scope has signaled that we should stop the scope
                    return true;
                }

                // Log the details we've been accumulating to help with debugging this scenario
                var leasePeriodMilliseconds = (int)_leasePeriod.TotalMilliseconds;
                var lastRenewalFormatted = _lastRenewal.ToString("yyyy-MM-ddTHH:mm:ss.FFFZ", CultureInfo.InvariantCulture);
                var millisecondsSinceLastSuccess = (int)(DateTime.UtcNow - _lastRenewal).TotalMilliseconds;
                var lastRenewalMilliseconds = (int)_lastRenewalLatency.TotalMilliseconds;

                var msg = string.Format(CultureInfo.InvariantCulture, "Singleton lock renewal failed for blob '{0}'. The last successful renewal completed at {1} ({2} milliseconds ago) with a duration of {3} milliseconds. The lease period was {4} milliseconds.",
                    LockId, lastRenewalFormatted, millisecondsSinceLastSuccess, lastRenewalMilliseconds, leasePeriodMilliseconds);
                log.Error(exception, msg);

                // If we've lost the lease or cannot re-establish it, we want to fail any
                // in progress function execution
                throw;

            }
        }
        public async Task ReleaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Note that this call returns without throwing if the lease is expired. See the table at:
                // http://msdn.microsoft.com/en-us/library/azure/ee691972.aspx
                await Blob.ReleaseAsync(
                    new BlobRequestConditions { LeaseId = LeaseId },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException exception)
            {
                if (exception.Status == 404 ||
                    exception.Status == 409)
                {
                    // if the blob no longer exists, or there is another lease
                    // now active, there is nothing for us to release so we can
                    // ignore
                }
                else
                {
                    throw;
                }
            }
        }
    }
}