using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using KnightBus.Azure.Storage.Singleton;
using KnightBus.Core.Singleton;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration;

[TestFixture]
public class BlobSingletonMembershipTests
{
    private static readonly IReadOnlyDictionary<string, int> LockWeights = Enumerable
        .Range(0, 8)
        .ToDictionary(i => $"Lock{i}", _ => 1);

    //Every membership is a host of its own, so each gets its own id within the one test process
    private static BlobSingletonMembership Create(
        IBlobLockScheme scheme,
        TimeProvider? time = null
    ) =>
        new(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            scheme,
            timeProvider: time,
            hostId: $"host-{Guid.NewGuid():N}"
        );

    //Each test gets its own lock directory, which is what separates the hosts of one test from another
    private static IBlobLockScheme NewScheme() => new TestLockScheme($"locks-{Guid.NewGuid():N}");

    private sealed class TestLockScheme(string directory) : IBlobLockScheme
    {
        public string ContainerName { get; } = new DefaultBlobLockScheme().ContainerName;
        public string Directory { get; } = directory;
    }

    private static BlobClient MemberBlob(IBlobLockScheme scheme, string hostId) =>
        new BlobContainerClient(StorageSetup.ConnectionString, scheme.ContainerName).GetBlobClient(
            $"{scheme.Directory.TrimEnd('/')}/_members/{hostId}"
        );

    private static List<string> MemberBlobNames(IBlobLockScheme scheme) =>
        new BlobContainerClient(StorageSetup.ConnectionString, scheme.ContainerName)
            .GetBlobs(
                BlobTraits.None,
                BlobStates.None,
                $"{scheme.Directory.TrimEnd('/')}/_members/",
                CancellationToken.None
            )
            .Select(b => b.Name)
            .ToList();

    [Test]
    public async Task Should_list_the_other_members_with_their_locks()
    {
        //arrange
        var scheme = NewScheme();
        var first = Create(scheme);
        var second = Create(scheme);
        await first.JoinAsync(LockWeights, CancellationToken.None);
        await second.JoinAsync(LockWeights, CancellationToken.None);

        //act
        var members = await first.GetMembersAsync(CancellationToken.None);

        //assert: this host knows its own locks, so it is not listed
        members.Should().ContainSingle().Which.HostId.Should().Be(second.HostId);
        members.Single().Locks.Should().BeEquivalentTo(LockWeights);

        await first.LeaveAsync(CancellationToken.None);
        await second.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_not_see_hosts_with_another_lock_scheme()
    {
        //arrange
        var one = Create(NewScheme());
        var other = Create(NewScheme());
        await one.JoinAsync(LockWeights, CancellationToken.None);
        await other.JoinAsync(LockWeights, CancellationToken.None);

        //act
        var members = await one.GetMembersAsync(CancellationToken.None);

        //assert
        members.Should().BeEmpty();

        await one.LeaveAsync(CancellationToken.None);
        await other.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_drop_a_host_that_leaves()
    {
        //arrange
        var scheme = NewScheme();
        var staying = Create(scheme);
        var leaving = Create(scheme);
        await staying.JoinAsync(LockWeights, CancellationToken.None);
        await leaving.JoinAsync(LockWeights, CancellationToken.None);
        (await staying.GetMembersAsync(CancellationToken.None)).Should().ContainSingle();

        //act
        await leaving.LeaveAsync(CancellationToken.None);

        //assert
        (await staying.GetMembersAsync(CancellationToken.None))
            .Should()
            .BeEmpty();
        MemberBlobNames(scheme).Should().ContainSingle().Which.Should().EndWith(staying.HostId);

        await staying.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_find_the_members_when_the_lock_directory_ends_with_a_slash()
    {
        //arrange
        var scheme = new TestLockScheme($"locks-{Guid.NewGuid():N}/");
        var first = Create(scheme);
        var second = Create(scheme);
        await first.JoinAsync(LockWeights, CancellationToken.None);
        await second.JoinAsync(LockWeights, CancellationToken.None);

        //act
        var members = await first.GetMembersAsync(CancellationToken.None);
        await second.LeaveAsync(CancellationToken.None);

        //assert: the hosts saw each other, and the one that left removed its member blob
        members.Select(m => m.HostId).Should().Contain(second.HostId);
        MemberBlobNames(scheme).Should().ContainSingle().Which.Should().EndWith(first.HostId);

        await first.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_not_count_a_member_blob_whose_lease_is_gone()
    {
        //arrange: what a crashed host leaves behind once its lease has expired
        var scheme = NewScheme();
        var membership = Create(scheme);
        await membership.JoinAsync(LockWeights, CancellationToken.None);
        await MemberBlob(scheme, "crashed-host")
            .UploadAsync(BinaryData.FromObjectAsJson(LockWeights), overwrite: true);

        //act
        var members = await membership.GetMembersAsync(CancellationToken.None);

        //assert: without a lease it is not alive
        members.Should().BeEmpty();

        await membership.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_remove_an_unleased_member_blob_only_once_it_has_not_been_written_for_a_while()
    {
        //arrange: what a crashed host leaves behind
        var time = new ManualTimeProvider();
        var scheme = NewScheme();
        var membership = Create(scheme, time);
        await membership.JoinAsync(LockWeights, CancellationToken.None);
        var stale = MemberBlob(scheme, "other-host");
        await stale.UploadAsync(BinaryData.FromObjectAsJson(new Dictionary<string, int>()), true);

        //act
        time.Advance(TimeSpan.FromMinutes(9));
        await membership.GetMembersAsync(CancellationToken.None);

        //assert
        (await stale.ExistsAsync())
            .Value.Should()
            .BeTrue("it was written less than 10 minutes ago");
        time.Advance(TimeSpan.FromMinutes(2));
        await membership.GetMembersAsync(CancellationToken.None);
        (await stale.ExistsAsync()).Value.Should().BeFalse();

        await membership.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_remove_a_member_blob_at_once_when_a_long_held_lease_has_gone()
    {
        //arrange: a host that held its member blob for a long time, then died
        var time = new ManualTimeProvider();
        var scheme = NewScheme();
        var membership = Create(scheme, time);
        await membership.JoinAsync(LockWeights, CancellationToken.None);
        var stale = MemberBlob(scheme, "other-host");
        await stale.UploadAsync(BinaryData.FromObjectAsJson(new Dictionary<string, int>()), true);
        var lease = stale.GetBlobLeaseClient();
        await lease.AcquireAsync(TimeSpan.FromSeconds(60));
        time.Advance(TimeSpan.FromMinutes(11));

        //act
        await lease.BreakAsync(TimeSpan.Zero);
        await membership.GetMembersAsync(CancellationToken.None);

        //assert: lease operations do not change the blob, so it is old enough already
        (await stale.ExistsAsync())
            .Value.Should()
            .BeFalse();

        await membership.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_not_remove_a_member_blob_that_was_written_again_after_the_listing()
    {
        //arrange: a sweeping host listed the blob as abandoned, then its owner rejoined and wrote it
        var scheme = NewScheme();
        var membership = Create(scheme);
        var member = MemberBlob(scheme, "rejoining-host");
        await member.GetParentBlobContainerClient().CreateIfNotExistsAsync();
        var listed = (await member.UploadAsync(BinaryData.FromString("{}"))).Value.ETag;
        await member.UploadAsync(BinaryData.FromString("{\"Lock0\":1}"), overwrite: true);

        //act
        await membership.TryDeleteAbandonedAsync(member.Name, listed, CancellationToken.None);

        //assert
        (await member.ExistsAsync())
            .Value.Should()
            .BeTrue();
        (await member.DownloadContentAsync()).Value.Content.ToString().Should().Be("{\"Lock0\":1}");
    }

    [Test]
    public async Task Should_join_again_when_the_member_lease_is_lost()
    {
        //arrange
        var scheme = NewScheme();
        var membership = Create(scheme);
        await membership.JoinAsync(LockWeights, CancellationToken.None);
        var member = MemberBlob(scheme, membership.HostId);

        //act: another host removes the blob once the lease has lapsed
        await member.GetBlobLeaseClient().BreakAsync(TimeSpan.Zero);
        await member.DeleteIfExistsAsync();
        var rejoined = false;
        for (var i = 0; i < 40 && !rejoined; i++)
        {
            await Task.Delay(1000);
            await membership.JoinAsync(LockWeights, CancellationToken.None);
            rejoined =
                await member.ExistsAsync()
                && (await member.GetPropertiesAsync()).Value.LeaseState == LeaseState.Leased;
        }

        //assert
        rejoined.Should().BeTrue();

        await membership.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_not_join_again_while_the_member_lease_is_alive()
    {
        //arrange
        var scheme = NewScheme();
        var membership = Create(scheme);
        await membership.JoinAsync(LockWeights, CancellationToken.None);
        var member = MemberBlob(scheme, membership.HostId);
        var etag = (await member.GetPropertiesAsync()).Value.ETag;

        //act
        await membership.JoinAsync(LockWeights, CancellationToken.None);

        //assert: the blob was not written again
        (await member.GetPropertiesAsync())
            .Value.ETag.Should()
            .Be(etag);

        await membership.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_keep_trying_to_join_again_after_a_failed_attempt()
    {
        //arrange: the member lease is lost, and something else holds the blob for now
        var scheme = NewScheme();
        var membership = Create(scheme);
        await membership.JoinAsync(LockWeights, CancellationToken.None);
        var member = MemberBlob(scheme, membership.HostId);
        await member.GetBlobLeaseClient().BreakAsync(TimeSpan.Zero);
        var blocker = member.GetBlobLeaseClient();
        await blocker.AcquireAsync(TimeSpan.FromSeconds(60));

        //act: wait until the renewal notices and the rejoin fails
        var failed = false;
        for (var i = 0; i < 40 && !failed; i++)
        {
            await Task.Delay(1000);
            try
            {
                await membership.JoinAsync(LockWeights, CancellationToken.None);
            }
            catch (Exception)
            {
                failed = true;
            }
        }
        await blocker.ReleaseAsync();
        await membership.JoinAsync(LockWeights, CancellationToken.None);

        //assert
        failed.Should().BeTrue("the rejoin cannot succeed while the blob is held");
        (await member.GetPropertiesAsync()).Value.LeaseState.Should().Be(LeaseState.Leased);

        await membership.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_join_after_a_failed_first_attempt()
    {
        //arrange: something else holds this host's member blob, so the first join fails
        var scheme = NewScheme();
        var membership = Create(scheme);
        var member = MemberBlob(scheme, membership.HostId);
        await member.GetParentBlobContainerClient().CreateIfNotExistsAsync();
        await member.UploadAsync(BinaryData.FromString("{}"));
        var blocker = member.GetBlobLeaseClient();
        await blocker.AcquireAsync(TimeSpan.FromSeconds(60));
        var join = () => membership.JoinAsync(LockWeights, CancellationToken.None);
        await join.Should().ThrowAsync<Exception>();

        //act
        await blocker.ReleaseAsync();
        await membership.JoinAsync(LockWeights, CancellationToken.None);

        //assert
        (await member.GetPropertiesAsync())
            .Value.LeaseState.Should()
            .Be(LeaseState.Leased);

        await membership.LeaveAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_let_the_lease_go_when_leaving_with_a_cancelled_token()
    {
        //arrange
        var scheme = NewScheme();
        var membership = Create(scheme);
        await membership.JoinAsync(LockWeights, CancellationToken.None);
        var member = MemberBlob(scheme, membership.HostId);

        //act
        await membership.LeaveAsync(new CancellationToken(canceled: true));

        //assert: the renewal stopped and the lease was released
        var released = false;
        for (var i = 0; i < 50 && !released; i++)
        {
            await Task.Delay(100);
            released =
                !await member.ExistsAsync()
                || (await member.GetPropertiesAsync()).Value.LeaseState != LeaseState.Leased;
        }
        released.Should().BeTrue();
    }

    [Test]
    public async Task Should_leave_without_having_joined()
    {
        var membership = Create(NewScheme());

        var leave = () => membership.LeaveAsync(CancellationToken.None);

        await leave.Should().NotThrowAsync();
    }

    [Test]
    public async Task Should_name_the_member_blob_with_forward_slashes()
    {
        var scheme = NewScheme();
        var membership = Create(scheme);
        await membership.JoinAsync(LockWeights, CancellationToken.None);

        var names = new BlobContainerClient(StorageSetup.ConnectionString, scheme.ContainerName)
            .GetBlobs(BlobTraits.None, BlobStates.None, scheme.Directory, CancellationToken.None)
            .Select(b => b.Name)
            .ToList();

        names
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be($"{scheme.Directory}/_members/{membership.HostId}");

        await membership.LeaveAsync(CancellationToken.None);
    }

    //Starts at the real time, like the storage service's clock, and moves only when advanced
    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _start = DateTimeOffset.UtcNow;
        private long _ticks;

        public override DateTimeOffset GetUtcNow() => _start.AddTicks(Interlocked.Read(ref _ticks));

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }
}
