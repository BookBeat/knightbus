using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using FluentAssertions;
using NUnit.Framework;
using KnightBus.Azure.Storage.Singleton;
using KnightBus.Core;
using Moq;

namespace KnightBus.Azure.Storage.Tests.Integration
{
    [TestFixture]
    public class BlobLockManagerTests
    {
        ///Azurite docker connection
        private string _connection ="UseDevelopmentStorage=true";

        [Test]
        [Parallelizable]
        public async Task Should_create_new_lock()
        {
            //arrange
            var lockManager = new BlobLockManager(_connection, new DefaultBlobLockScheme());
            await lockManager.InitializeAsync();
            var lockId = Guid.NewGuid().ToString();
            //act
            var handle = await lockManager.TryLockAsync(lockId, TimeSpan.FromMinutes(1), CancellationToken.None);
            //assert
            handle.Should().NotBeNull("Lock should be acquired");
            handle.LeaseId.Should().NotBeNullOrWhiteSpace();
            handle.LockId.Should().NotBeNullOrWhiteSpace();
        }
        
        [Test]
        [Parallelizable]
        public async Task Should_not_get_lease_when_already_locked()
        {
            //arrange
            var lockManager = new BlobLockManager(_connection, new DefaultBlobLockScheme());
            await lockManager.InitializeAsync();
            var lockId = Guid.NewGuid().ToString();
            await lockManager.TryLockAsync(lockId, TimeSpan.FromMinutes(1), CancellationToken.None);
            //act
            var secondHandle = await lockManager.TryLockAsync(lockId, TimeSpan.FromMinutes(1), CancellationToken.None);
            //assert
            secondHandle.Should().BeNull("Already locked");
        }
        
        [Test]
        [Parallelizable]
        public async Task Should_release_lease()
        {
            //arrange
            var lockManager = new BlobLockManager(_connection, new DefaultBlobLockScheme());
            await lockManager.InitializeAsync();
            var lockId = Guid.NewGuid().ToString();
            var handle = await lockManager.TryLockAsync(lockId, TimeSpan.FromMinutes(1), CancellationToken.None);
            //act
            await handle.ReleaseAsync(CancellationToken.None);
            var secondHandle = await lockManager.TryLockAsync(lockId, TimeSpan.FromMinutes(1), CancellationToken.None);
            //assert
            secondHandle.Should().NotBeNull();
        }
        
        [Test]
        [Parallelizable]
        public async Task Should_renew_lease()
        {
            //arrange
            var lockManager = new BlobLockManager(_connection, new DefaultBlobLockScheme());
            await lockManager.InitializeAsync();
            var lockId = Guid.NewGuid().ToString();
            var handle = await lockManager.TryLockAsync(lockId, TimeSpan.FromSeconds(15), CancellationToken.None);
            //act
            await Task.Delay(TimeSpan.FromSeconds(10));
            var renewed = await handle.RenewAsync(Mock.Of<ILog>(), CancellationToken.None);
            //assert
            renewed.Should().BeTrue();
        }
        
        [Test]
        [Parallelizable]
        public async Task Should_not_renew_expired_lease()
        {
            //arrange
            var lockManager = new BlobLockManager(_connection, new DefaultBlobLockScheme());
            await lockManager.InitializeAsync();
            var lockId = Guid.NewGuid().ToString();
            var handle = await lockManager.TryLockAsync(lockId, TimeSpan.FromSeconds(15), CancellationToken.None);
            //act
            await Task.Delay(TimeSpan.FromSeconds(16));
            //steal lock
            await lockManager.TryLockAsync(lockId, TimeSpan.FromSeconds(15), CancellationToken.None);
            handle.Awaiting(x=> x.RenewAsync(Mock.Of<ILog>(), CancellationToken.None)).Should().Throw<RequestFailedException>();
        }
    }
}