using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using KnightBus.Azure.Storage.Singleton;
namespace KnightBus.Azure.Storage.Tests.Integration
{
    [TestFixture]
    public class BlobLockManagerTests
    {
        ///Azurite docker connection
        private string _connection ="UseDevelopmentStorage=true";

        [Test]
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
        public async Task Should_not_get_lease_when_already_locked()
        {
            //arrange
            var lockManager = new BlobLockManager(_connection, new DefaultBlobLockScheme());
            await lockManager.InitializeAsync();
            var lockId = Guid.NewGuid().ToString();
            //act
            await lockManager.TryLockAsync(lockId, TimeSpan.FromMinutes(1), CancellationToken.None);
            var secondHandle = await lockManager.TryLockAsync(lockId, TimeSpan.FromMinutes(1), CancellationToken.None);
            //assert
            secondHandle.Should().BeNull("Already locked");
        }
    }
}