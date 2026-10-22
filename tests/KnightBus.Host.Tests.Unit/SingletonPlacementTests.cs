using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Core.Singleton;
using KnightBus.Host.Singleton;
using KnightBus.Tests.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

[TestFixture]
public class SingletonPlacementTests
{
    private static readonly IReadOnlyDictionary<string, int> Locks = Enumerable
        .Range(0, 8)
        .ToDictionary(i => $"Lock{i}", _ => 1);

    private static readonly IReadOnlyDictionary<string, int> NoLocks =
        new Dictionary<string, int>();

    private static SingletonPlacement Create(
        Membership membership,
        ILogger? log = null,
        TimeProvider? time = null,
        TimeSpan? refreshInterval = null
    ) =>
        new(membership, log ?? Mock.Of<ILogger>(), time)
        {
            //The loop is not what most tests are about, they call RefreshAsync themselves
            RefreshInterval = refreshInterval ?? TimeSpan.FromMinutes(10),
            StaleAfter = TimeSpan.FromMinutes(30),
        };

    [Test]
    public async Task Should_agree_with_the_other_hosts_on_every_lock()
    {
        //arrange
        var members = new Members();
        var first = Create(new Membership(members, "first"));
        var second = Create(new Membership(members, "second"));
        await first.StartAsync(Locks, CancellationToken.None);
        await second.StartAsync(Locks, CancellationToken.None);

        //act: the first host started before the second joined, so it has to look again
        await first.RefreshAsync(CancellationToken.None);

        //assert
        foreach (var lockId in Locks.Keys)
            first.Advise(lockId).Should().Be(second.Advise(lockId), lockId);
        Locks
            .Keys.Select(first.Advise)
            .Distinct()
            .Should()
            .BeEquivalentTo(["first", "second"], "with 8 locks both hosts should get a share");

        await first.StopAsync(CancellationToken.None);
        await second.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_count_this_host_before_the_others_can_see_it()
    {
        //arrange: this host has not reached the members yet
        var members = new Members();
        members.Join("other", Locks);
        var placement = Create(new Membership(members, "me"));

        //act
        await placement.StartAsync(Locks, CancellationToken.None);

        //assert
        Locks.Keys.Select(placement.Advise).Should().Contain("me");

        await placement.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_stop_advising_when_the_members_have_not_been_read_for_too_long()
    {
        //arrange
        var time = new ManualTime();
        var placement = Create(new Membership(new Members(), "me"), time: time);
        await placement.StartAsync(Locks, CancellationToken.None);
        placement.Advise("Lock0").Should().Be("me");

        //act: no refresh succeeds for longer than the stale limit
        time.Advance(TimeSpan.FromMinutes(31));

        //assert: no information means the caller acts as if there were no placement
        placement.Advise("Lock0").Should().BeNull();

        await placement.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_keep_its_advice_through_failed_refreshes_until_it_goes_stale()
    {
        //arrange
        var log = new ListLogger();
        var time = new ManualTime();
        var membership = new Membership(new Members(), "me");
        var placement = Create(
            membership,
            log,
            time,
            refreshInterval: TimeSpan.FromMilliseconds(20)
        );
        await placement.StartAsync(Locks, CancellationToken.None);
        placement.Advise("Lock0").Should().Be("me");

        //act
        membership.FailReads = true;
        await Eventually.True(() =>
            log.Messages.Count(m => m == "Could not read the singleton placement members") >= 3
        );

        //assert: one storage error must not send every host back to competing for every lock
        placement.Advise("Lock0").Should().Be("me");
        time.Advance(TimeSpan.FromMinutes(31));
        placement.Advise("Lock0").Should().BeNull();

        await placement.StopAsync(CancellationToken.None);
    }

    [Test]
    public void Should_outlast_more_than_one_failed_refresh_before_going_stale()
    {
        var placement = new SingletonPlacement(
            new Membership(new Members(), "me"),
            Mock.Of<ILogger>()
        );

        placement.StaleAfter.Should().BeGreaterThan(placement.RefreshInterval * 2);
    }

    [Test]
    public async Task Should_give_no_advice_for_unknown_locks_or_before_it_has_started()
    {
        var placement = Create(new Membership(new Members(), "me"));
        placement.Advise("Lock0").Should().BeNull("not started");

        await placement.StartAsync(Locks, CancellationToken.None);
        placement.Advise("Unregistered").Should().BeNull();

        await placement.StopAsync(CancellationToken.None);
        placement.Advise("Lock0").Should().BeNull("stopped");
    }

    [Test]
    public async Task Should_not_join_when_the_host_has_no_singleton_processors()
    {
        var membership = new Membership(new Members(), "me");
        var placement = Create(membership);

        await placement.StartAsync(NoLocks, CancellationToken.None);
        await placement.StopAsync(CancellationToken.None);

        membership.Joins.Should().Be(0);
        membership.Leaves.Should().Be(0);
    }

    [Test]
    public async Task Should_join_later_when_joining_at_start_fails()
    {
        //arrange
        var log = new ListLogger();
        var membership = new Membership(new Members(), "me") { FailJoins = true };
        var placement = Create(membership, log, refreshInterval: TimeSpan.FromMilliseconds(20));

        //act
        var start = () => placement.StartAsync(Locks, CancellationToken.None);

        //assert: starting does not fail, and the host gives no advice until it has joined
        await start.Should().NotThrowAsync();
        placement.Advise("Lock0").Should().BeNull();
        log.Messages.Should()
            .Contain("Host me could not join the singleton placement yet, it keeps trying");

        membership.FailJoins = false;
        await Eventually.True(() => placement.Advise("Lock0") == "me");

        await placement.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_say_it_joined_when_only_reading_the_members_fails()
    {
        //arrange
        var log = new ListLogger();
        var membership = new Membership(new Members(), "me") { FailReads = true };
        var placement = Create(membership, log);

        //act
        await placement.StartAsync(Locks, CancellationToken.None);

        //assert
        log.Messages.Should()
            .Contain("Host me joined the singleton placement with 8 singleton processors")
            .And.Contain(
                "Host me could not read the singleton placement members yet, it keeps trying"
            )
            .And.NotContain(m => m.Contains("could not join"));

        await placement.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_join_again_on_every_refresh()
    {
        //arrange: the membership decides whether a join is needed
        var membership = new Membership(new Members(), "me");
        var placement = Create(membership, refreshInterval: TimeSpan.FromMilliseconds(20));

        //act
        await placement.StartAsync(Locks, CancellationToken.None);

        //assert
        await Eventually.True(() => membership.Joins >= 3);

        await placement.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_leave_when_stopping_while_a_refresh_is_stuck()
    {
        //arrange: a refresh that ignores its cancellation
        var membership = new Membership(new Members(), "me");
        var placement = Create(membership, refreshInterval: TimeSpan.FromMilliseconds(20));
        await placement.StartAsync(Locks, CancellationToken.None);
        membership.Stuck = new TaskCompletionSource();
        await Eventually.True(() => membership.StuckReads > 0);

        //act
        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await placement.StopAsync(stopping.Token);

        //assert
        membership.Leaves.Should().Be(1);
        membership.Stuck.SetResult();
    }

    [Test]
    public async Task Should_leave_when_stopping_with_a_cancelled_token()
    {
        var membership = new Membership(new Members(), "me");
        var placement = Create(membership);
        await placement.StartAsync(Locks, CancellationToken.None);

        await placement.StopAsync(new CancellationToken(canceled: true));

        membership.Leaves.Should().Be(1);
    }

    //What every host sees, like the member blobs
    private sealed class Members
    {
        private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, int>> _members =
            new();

        public void Join(string hostId, IReadOnlyDictionary<string, int> locks) =>
            _members[hostId] = locks;

        public void Leave(string hostId) => _members.TryRemove(hostId, out _);

        public IReadOnlyCollection<SingletonMember> All() =>
            _members.Select(x => new SingletonMember(x.Key, x.Value)).ToList();
    }

    private sealed class Membership(Members members, string hostId) : ISingletonMembership
    {
        private int _joins;
        private int _leaves;
        private int _stuckReads;
        public int Joins => _joins;
        public int Leaves => _leaves;
        public int StuckReads => _stuckReads;
        public volatile bool FailJoins;
        public volatile bool FailReads;

        //Reading the members waits for this, whatever its token says
        public volatile TaskCompletionSource? Stuck;

        public string HostId => hostId;

        public Task JoinAsync(
            IReadOnlyDictionary<string, int> locks,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref _joins);
            if (FailJoins)
                throw new InvalidOperationException("join");
            members.Join(hostId, locks);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyCollection<SingletonMember>> GetMembersAsync(
            CancellationToken cancellationToken
        )
        {
            if (FailReads)
                throw new InvalidOperationException("read");
            if (Stuck is { } stuck)
            {
                Interlocked.Increment(ref _stuckReads);
                await stuck.Task;
            }
            return members.All().Where(m => m.HostId != hostId).ToList();
        }

        public Task LeaveAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _leaves);
            members.Leave(hostId);
            return Task.CompletedTask;
        }
    }
}
