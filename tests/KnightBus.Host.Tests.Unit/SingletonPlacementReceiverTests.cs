using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using KnightBus.Host.Singleton;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

[TestFixture]
public class SingletonPlacementReceiverTests
{
    private const string Lock = "TheLock";
    private const string Me = "me";
    private const string Other = "other";

    private static readonly SingletonPlacementOptions Options = new()
    {
        TakeoverGrace = TimeSpan.FromMinutes(2),
        StabilityWindow = TimeSpan.FromMinutes(2),
        HandoffInterval = TimeSpan.FromMinutes(10),
        DrainTimeout = TimeSpan.FromMinutes(5),
    };

    private sealed class Fixture : IAsyncDisposable
    {
        public readonly ManualTime Time = new();
        public readonly FakePlacement Placement = new();
        public readonly InMemoryLocks Locks = new();
        public readonly FakeReceiver Inner = new();
        public readonly CancellationTokenSource Shutdown = new();
        public SingletonChannelReceiver Receiver { get; }

        public Fixture(
            SingletonPlacementOptions? options = null,
            bool drainable = true,
            bool inspectable = true
        )
        {
            Inner.Drainable = drainable;
            Receiver = new SingletonChannelReceiver(
                drainable ? Inner : new NonDrainable(Inner),
                inspectable ? Locks : new NotInspectable(Locks),
                Mock.Of<ILogger>(),
                Lock,
                null,
                Placement,
                options ?? Options
            )
            {
                TimerInterval = TimeSpan.FromMilliseconds(20),
                LockRefreshInterval = TimeSpan.FromMilliseconds(20),
                Time = Time,
            };
        }

        public Task Start() => Receiver.StartAsync(Shutdown.Token);

        public ValueTask DisposeAsync()
        {
            Shutdown.Cancel();
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task Should_take_the_lock_right_away_when_this_host_is_preferred()
    {
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me, self: true);

        await f.Start();

        f.Locks.Holder(Lock).Should().Be(Me);
        f.Inner.Started.Should().Be(1);
    }

    [Test]
    public async Task Should_take_the_lock_right_away_when_there_is_no_advice()
    {
        //arrange: the placement could not read the group
        await using var f = new Fixture();

        //act
        await f.Start();

        //assert: acts as if there were no placement
        f.Locks.Holder(Lock).Should().Be(Me);
    }

    [Test]
    public async Task Should_leave_the_lock_to_the_preferred_host_for_the_grace_period()
    {
        //arrange
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Other, self: false);

        //act: the lock is free, but another host should have it
        await f.Start();
        await Task.Delay(200);

        //assert
        f.Locks.Holder(Lock).Should().BeNull();
        f.Locks.Attempts.Should().Be(0);
    }

    [Test]
    public async Task Should_take_the_lock_after_the_grace_period_when_the_preferred_host_does_not()
    {
        //arrange
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Other, self: false);
        await f.Start();

        //act
        f.Time.Advance(TimeSpan.FromMinutes(2));
        await Eventually(() => f.Locks.Holder(Lock) == Me);

        //assert
        f.Inner.Started.Should().Be(1);
    }

    [Test]
    public async Task Should_measure_the_grace_period_from_when_the_lock_became_free()
    {
        //arrange: another host holds the lock for a long time while it is the one that should not have it
        await using var f = new Fixture();
        f.Locks.Take(Lock, Other);
        f.Placement.Prefer(Lock, Me, self: false);
        await f.Start();
        f.Time.Advance(TimeSpan.FromHours(1));
        await Task.Delay(100);

        //act: the holder lets go, but this host is not the one that should have it
        f.Locks.ReleaseFor(Lock);
        await Task.Delay(300);

        //assert: the hour of waiting does not count, the lock has only just been free
        f.Locks.Holder(Lock).Should().BeNull();
        f.Time.Advance(TimeSpan.FromMinutes(2));
        await Eventually(() => f.Locks.Holder(Lock) == Me);
    }

    [Test]
    public async Task Should_leave_the_lock_alone_when_it_cannot_tell_whether_it_is_held()
    {
        //arrange: a lock manager that cannot be inspected
        var f = new Fixture(inspectable: false);
        await using var _ = f;
        f.Placement.Prefer(Lock, Other, self: false);

        //act
        await f.Start();
        f.Time.Advance(TimeSpan.FromHours(1));
        await Task.Delay(300);

        //assert
        f.Locks.Attempts.Should().Be(0);
    }

    [Test]
    public async Task Should_hand_the_lock_over_once_the_preferred_host_has_settled()
    {
        //arrange: this host holds the lock, then another host becomes the preferred one
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me, self: true);
        await f.Start();
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Prefer(Lock, Other, self: false, memberFor: TimeSpan.FromMinutes(3));
        await Eventually(() => f.Locks.Holder(Lock) == null);

        //assert: the receiver was stopped before the lock was released
        f.Inner.StopFetchingCalls.Should().BeGreaterThan(0);
        f.Inner.Token!.Value.IsCancellationRequested.Should().BeTrue();
        //and it leaves the lock alone for the grace period so the other host gets there first
        await Task.Delay(200);
        f.Locks.Holder(Lock).Should().BeNull();
    }

    [Test]
    public async Task Should_not_hand_over_to_a_host_that_has_only_just_joined()
    {
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me, self: true);
        await f.Start();
        f.Time.Advance(TimeSpan.FromMinutes(11));

        f.Placement.Prefer(Lock, Other, self: false, memberFor: TimeSpan.FromSeconds(30));
        await Task.Delay(300);

        f.Locks.Holder(Lock).Should().Be(Me);
        f.Inner.StopFetchingCalls.Should().Be(0);
    }

    [Test]
    public async Task Should_not_hand_over_a_lock_it_has_only_just_taken()
    {
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me, self: true);
        await f.Start();
        f.Time.Advance(TimeSpan.FromMinutes(1));

        f.Placement.Prefer(Lock, Other, self: false, memberFor: TimeSpan.FromMinutes(5));
        await Task.Delay(300);

        f.Locks.Holder(Lock).Should().Be(Me);
    }

    [Test]
    public async Task Should_let_the_running_message_finish_before_cancelling_it()
    {
        //arrange
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me, self: true);
        await f.Start();
        f.Inner.HoldDrain = new TaskCompletionSource();
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Prefer(Lock, Other, self: false, memberFor: TimeSpan.FromMinutes(3));
        await Eventually(() => f.Inner.StopFetchingCalls == 1);
        await Task.Delay(200);

        //assert: still draining, so nothing has been cancelled or released
        f.Inner.Token!.Value.IsCancellationRequested.Should().BeFalse();
        f.Locks.Holder(Lock).Should().Be(Me);

        f.Inner.HoldDrain.SetResult();
        await Eventually(() => f.Locks.Holder(Lock) == null);
        f.Inner.Token!.Value.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public async Task Should_cancel_a_message_that_outlasts_the_drain_timeout()
    {
        //arrange
        var options = new SingletonPlacementOptions
        {
            StabilityWindow = Options.StabilityWindow,
            HandoffInterval = Options.HandoffInterval,
            TakeoverGrace = Options.TakeoverGrace,
            DrainTimeout = TimeSpan.FromMilliseconds(150),
        };
        await using var f = new Fixture(options);
        f.Placement.Prefer(Lock, Me, self: true);
        await f.Start();
        f.Inner.HoldDrain = new TaskCompletionSource(); //never completes
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Prefer(Lock, Other, self: false, memberFor: TimeSpan.FromMinutes(3));

        //assert
        await Eventually(() => f.Locks.Holder(Lock) == null);
        f.Inner.Token!.Value.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public async Task Should_cancel_and_release_when_the_receiver_cannot_drain()
    {
        await using var f = new Fixture(drainable: false);
        f.Placement.Prefer(Lock, Me, self: true);
        await f.Start();
        f.Time.Advance(TimeSpan.FromMinutes(11));

        f.Placement.Prefer(Lock, Other, self: false, memberFor: TimeSpan.FromMinutes(3));

        await Eventually(() => f.Locks.Holder(Lock) == null);
        f.Inner.StopFetchingCalls.Should().Be(0);
        f.Inner.Token!.Value.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public async Task Should_keep_the_lock_when_the_advice_goes_away()
    {
        //arrange: the placement lost sight of the group while this host holds the lock
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me, self: true);
        await f.Start();
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Clear(Lock);
        await Task.Delay(300);

        //assert
        f.Locks.Holder(Lock).Should().Be(Me);
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Timed out waiting for the condition");
            await Task.Delay(10);
        }
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }

    private sealed class FakePlacement : ISingletonPlacement
    {
        private readonly ConcurrentDictionary<string, SingletonPlacementAdvice> _advice = new();

        public string HostId => Me;

        public void Prefer(string lockId, string host, bool self, TimeSpan? memberFor = null) =>
            _advice[lockId] = new SingletonPlacementAdvice(host, self, memberFor ?? TimeSpan.Zero);

        public void Clear(string lockId) => _advice.TryRemove(lockId, out _);

        public void Register(string lockId, int weight) { }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public SingletonPlacementAdvice? Advise(string lockId) =>
            _advice.TryGetValue(lockId, out var advice) ? advice : null;
    }

    private sealed class InMemoryLocks : ISingletonLockManager, ISingletonLockInspector
    {
        private readonly ConcurrentDictionary<string, string> _holders = new();
        private int _attempts;
        public int Attempts => _attempts;

        public void Take(string lockId, string host) => _holders[lockId] = host;

        public void ReleaseFor(string lockId) => _holders.TryRemove(lockId, out _);

        public string? Holder(string lockId) =>
            _holders.TryGetValue(lockId, out var holder) ? holder : null;

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<bool> IsHeldAsync(string lockId, CancellationToken cancellationToken) =>
            Task.FromResult(_holders.ContainsKey(lockId));

        public Task<ISingletonLockHandle?> TryLockAsync(
            string lockId,
            TimeSpan lockPeriod,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref _attempts);
            return Task.FromResult<ISingletonLockHandle?>(
                _holders.TryAdd(lockId, Me) ? new Handle(this, lockId) : null
            );
        }

        private sealed class Handle(InMemoryLocks owner, string lockId) : ISingletonLockHandle
        {
            public string LeaseId => Me;
            public string LockId => lockId;

            public Task<bool> RenewAsync(ILogger log, CancellationToken cancellationToken) =>
                Task.FromResult(true);

            public Task ReleaseAsync(CancellationToken cancellationToken)
            {
                owner._holders.TryRemove(lockId, out _);
                return Task.CompletedTask;
            }
        }
    }

    private class FakeReceiver : IDrainableChannelReceiver
    {
        private int _started;
        private int _stopFetchingCalls;
        public bool Drainable { get; set; } = true;
        public int Started => _started;
        public int StopFetchingCalls => _stopFetchingCalls;
        public CancellationToken? Token { get; private set; }
        public TaskCompletionSource? HoldDrain { get; set; }
        public IProcessingSettings Settings { get; set; } = new SingletonProcessingSettings();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _started);
            Token = cancellationToken;
            return Task.CompletedTask;
        }

        public async Task StopFetchingAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _stopFetchingCalls);
            //A message that is cancelled unwinds, which is what the second call waits for
            if (HoldDrain != null && Token?.IsCancellationRequested != true)
                await HoldDrain.Task.WaitAsync(cancellationToken);
        }
    }

    /// <summary>A lock manager that can only take locks, like a custom implementation</summary>
    private sealed class NotInspectable(ISingletonLockManager inner) : ISingletonLockManager
    {
        public Task InitializeAsync() => inner.InitializeAsync();

        public Task<ISingletonLockHandle?> TryLockAsync(
            string lockId,
            TimeSpan lockPeriod,
            CancellationToken cancellationToken
        ) => inner.TryLockAsync(lockId, lockPeriod, cancellationToken);
    }

    /// <summary>A receiver that only has the plain interface, like the transports that cannot drain</summary>
    private sealed class NonDrainable(FakeReceiver inner) : IChannelReceiver
    {
        public IProcessingSettings Settings
        {
            get => inner.Settings;
            set => inner.Settings = value;
        }

        public Task StartAsync(CancellationToken cancellationToken) =>
            inner.StartAsync(cancellationToken);
    }
}
