using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using KnightBus.Azure.Storage.Singleton;
using KnightBus.Core.Singleton;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration;

[TestFixture]
public class BlobSingletonPlacementTests
{
    private static readonly string[] Locks = Enumerable
        .Range(0, 8)
        .Select(i => $"Lock{i}")
        .ToArray();

    private static BlobSingletonPlacement Create(
        string group,
        TimeProvider? time = null,
        Action<SingletonPlacementOptions>? configure = null
    )
    {
        var options = new SingletonPlacementOptions
        {
            Group = group,
            //The background loop is not what is under test, the tests call RefreshAsync themselves
            RefreshInterval = TimeSpan.FromMinutes(10),
        };
        configure?.Invoke(options);
        var placement = new BlobSingletonPlacement(
            new StorageBusConfiguration(StorageSetup.ConnectionString),
            options,
            new DefaultBlobLockScheme(),
            timeProvider: time
        );
        foreach (var lockId in Locks)
            placement.Register(lockId, 1);
        return placement;
    }

    private static string NewGroup() => $"test-{Guid.NewGuid():N}";

    [Test]
    public async Task Should_agree_with_the_other_hosts_in_the_group_on_every_lock()
    {
        //arrange
        var group = NewGroup();
        var first = Create(group);
        var second = Create(group);
        await first.StartAsync(CancellationToken.None);
        await second.StartAsync(CancellationToken.None);

        //act: the first host started before the second joined, so it has to look again
        await first.RefreshAsync(CancellationToken.None);

        //assert
        foreach (var lockId in Locks)
        {
            var a = first.Advise(lockId)!;
            var b = second.Advise(lockId)!;
            a.PreferredHost.Should().Be(b.PreferredHost, lockId);
            a.PreferredIsSelf.Should().Be(a.PreferredHost == first.HostId);
            b.PreferredIsSelf.Should().Be(b.PreferredHost == second.HostId);
        }
        Locks
            .Select(l => first.Advise(l)!.PreferredHost)
            .Distinct()
            .Should()
            .BeEquivalentTo(
                [first.HostId, second.HostId],
                "with 8 locks both hosts should get a share"
            );

        await first.StopAsync(CancellationToken.None);
        await second.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_not_see_hosts_in_another_group()
    {
        //arrange
        var one = Create(NewGroup());
        var other = Create(NewGroup());
        await one.StartAsync(CancellationToken.None);
        await other.StartAsync(CancellationToken.None);

        //act
        await one.RefreshAsync(CancellationToken.None);

        //assert
        Locks.Select(l => one.Advise(l)!.PreferredHost).Should().OnlyContain(h => h == one.HostId);

        await one.StopAsync(CancellationToken.None);
        await other.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_drop_a_host_that_leaves_the_group()
    {
        //arrange
        var group = NewGroup();
        var staying = Create(group);
        var leaving = Create(group);
        await staying.StartAsync(CancellationToken.None);
        await leaving.StartAsync(CancellationToken.None);
        await staying.RefreshAsync(CancellationToken.None);
        Locks
            .Select(l => staying.Advise(l)!.PreferredHost)
            .Should()
            .Contain(leaving.HostId, "the leaving host had a share before it left");

        //act
        await leaving.StopAsync(CancellationToken.None);
        await staying.RefreshAsync(CancellationToken.None);

        //assert
        Locks
            .Select(l => staying.Advise(l)!.PreferredHost)
            .Should()
            .OnlyContain(h => h == staying.HostId);
        var remaining = new BlobContainerClient(
            StorageSetup.ConnectionString,
            new DefaultBlobLockScheme().ContainerName
        )
            .GetBlobs(
                BlobTraits.None,
                BlobStates.None,
                $"locks/_members/{group}/",
                CancellationToken.None
            )
            .Select(b => b.Name)
            .ToList();
        remaining.Should().ContainSingle().Which.Should().EndWith(staying.HostId);

        await staying.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_not_count_a_member_blob_whose_lease_is_gone()
    {
        //arrange: what a crashed host leaves behind once its lease has expired
        var group = NewGroup();
        var placement = Create(group);
        await placement.StartAsync(CancellationToken.None);
        var container = new BlobContainerClient(
            StorageSetup.ConnectionString,
            new DefaultBlobLockScheme().ContainerName
        );
        await container
            .GetBlobClient($"locks/_members/{group}/crashed-host")
            .UploadAsync(
                BinaryData.FromObjectAsJson(Locks.ToDictionary(l => l, _ => 1)),
                overwrite: true
            );

        //act
        await placement.RefreshAsync(CancellationToken.None);

        //assert: without a lease it is not alive, so it gets no locks
        Locks
            .Select(l => placement.Advise(l)!.PreferredHost)
            .Should()
            .OnlyContain(h => h == placement.HostId);

        await placement.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_report_how_long_the_preferred_host_has_been_a_member()
    {
        //arrange
        var time = new ManualTimeProvider();
        var group = NewGroup();
        var first = Create(group, time);
        var second = Create(group);
        await first.StartAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(3));
        await second.StartAsync(CancellationToken.None);

        //act
        await first.RefreshAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(30));

        //assert: the host that was there from the start has been a member longer than the newcomer
        var own = Locks.Select(l => first.Advise(l)!).First(a => a.PreferredIsSelf);
        var theirs = Locks.Select(l => first.Advise(l)!).First(a => !a.PreferredIsSelf);
        own.PreferredHostMemberFor.Should().Be(TimeSpan.FromMinutes(3.5));
        theirs.PreferredHostMemberFor.Should().Be(TimeSpan.FromSeconds(30));

        await first.StopAsync(CancellationToken.None);
        await second.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_stop_advising_when_the_group_has_not_been_read_for_too_long()
    {
        //arrange
        var time = new ManualTimeProvider();
        var placement = Create(NewGroup(), time, o => o.StaleAfter = TimeSpan.FromMinutes(30));
        await placement.StartAsync(CancellationToken.None);
        placement.Advise("Lock0").Should().NotBeNull();

        //act: no refresh succeeds for longer than the stale limit
        time.Advance(TimeSpan.FromMinutes(31));

        //assert: no information means the caller acts as if there were no placement
        placement.Advise("Lock0").Should().BeNull();

        await placement.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_give_no_advice_for_unknown_locks_or_before_it_has_started()
    {
        var placement = Create(NewGroup());
        placement.Advise("Lock0").Should().BeNull("not started");

        await placement.StartAsync(CancellationToken.None);
        placement.Advise("Unregistered").Should().BeNull();

        await placement.StopAsync(CancellationToken.None);
        placement.Advise("Lock0").Should().BeNull("stopped");
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }
}
