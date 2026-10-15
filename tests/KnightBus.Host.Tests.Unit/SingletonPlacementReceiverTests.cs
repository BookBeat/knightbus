using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using KnightBus.Host.Singleton;
using KnightBus.Messages;
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

    private sealed class Fixture : IAsyncDisposable
    {
        public readonly ManualTime Time = new();
        public readonly FakePlacement Placement = new();
        public readonly InMemoryLocks Locks = new();
        public readonly FakeReceiver Inner = new();
        public readonly CancellationTokenSource Shutdown = new();
        public readonly GatedProcessor Processor = new();
        public readonly InFlightMessages InFlight;
        public SingletonChannelReceiver Receiver { get; }

        public Fixture(
            bool drainable = true,
            bool inspectable = true,
            TimeSpan? lockTimeout = null,
            ILogger? log = null
        )
        {
            Inner.Drainable = drainable;
            InFlight = new InFlightMessages(Processor);
            //A hand-over lets the running message finish for as long as its lock lasts
            Inner.Settings = new SingletonProcessingSettings
            {
                MessageLockTimeout = lockTimeout ?? TimeSpan.FromMinutes(5),
            };
            Receiver = new SingletonChannelReceiver(
                drainable ? Inner : new NonDrainable(Inner),
                inspectable ? Locks : new NotInspectable(Locks),
                log ?? Mock.Of<ILogger>(),
                Lock,
                null,
                Placement,
                InFlight
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
        f.Placement.Prefer(Lock, Me);

        await f.Start();

        f.Locks.Holder(Lock).Should().Be(Me);
        f.Inner.Started.Should().Be(1);
    }

    [Test]
    public async Task Should_keep_polling_after_the_lock_manager_fails()
    {
        //arrange: another host holds the lock, and the next attempts fail
        await using var f = new Fixture();
        f.Locks.Take(Lock, Other);
        await f.Start();
        f.Locks.FailNext(3);

        //act: the other host goes away once the failures are over
        await Eventually(() => f.Locks.Attempts >= 5);
        f.Locks.ReleaseFor(Lock);

        //assert
        await Eventually(() => f.Locks.Holder(Lock) == Me);
        f.Inner.Started.Should().Be(1);
    }

    [Test]
    public async Task Should_look_for_a_lock_it_should_hold_more_often_than_the_poll_interval()
    {
        //arrange: another host holds the lock this one should have, and the poll interval is long
        await using var f = new Fixture();
        f.Receiver.TimerInterval = TimeSpan.FromMinutes(1);
        f.Receiver.PreferredPollInterval = TimeSpan.FromMilliseconds(20);
        f.Locks.Take(Lock, Other);
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        await Eventually(() => f.Locks.Attempts >= 2);

        //act: the other host hands it over after this host has polled and is waiting again
        f.Locks.ReleaseFor(Lock);

        //assert: picked up well within one poll interval
        await Eventually(() => f.Locks.Holder(Lock) == Me);
    }

    [Test]
    public async Task Should_keep_the_poll_interval_for_a_lock_another_host_should_hold()
    {
        //arrange
        await using var f = new Fixture();
        f.Receiver.TimerInterval = TimeSpan.FromMinutes(1);
        f.Receiver.PreferredPollInterval = TimeSpan.FromMilliseconds(20);
        f.Locks.Take(Lock, Other);
        f.Placement.Prefer(Lock, Other);

        //act
        await f.Start();
        await Task.Delay(300);

        //assert: only the look at start, nothing until the poll interval has passed
        f.Locks.Inspections.Should().Be(1);
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
        f.Placement.Prefer(Lock, Other);

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
        f.Placement.Prefer(Lock, Other);
        await f.Start();

        //act
        f.Time.Advance(TimeSpan.FromMinutes(2));
        await Eventually(() => f.Locks.Holder(Lock) == Me);

        //assert: the receiver starts right after the lock is taken
        await Eventually(() => f.Inner.Started == 1);
    }

    [Test]
    public async Task Should_measure_the_grace_period_from_when_the_lock_became_free()
    {
        //arrange: another host holds the lock for a long time, and a third host should have it
        await using var f = new Fixture();
        f.Locks.Take(Lock, Other);
        f.Placement.Prefer(Lock, "third");
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
        f.Placement.Prefer(Lock, Other);

        //act
        await f.Start();
        f.Time.Advance(TimeSpan.FromHours(1));
        await Task.Delay(300);

        //assert
        f.Locks.Attempts.Should().Be(0);
    }

    [Test]
    public async Task Should_hand_the_lock_over_once_it_has_been_held_long_enough()
    {
        //arrange: this host holds the lock, then another host becomes the preferred one
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Prefer(Lock, Other);
        await Eventually(() => f.Locks.Holder(Lock) == null);

        //assert: the receiver was stopped before the lock was released
        f.Inner.StopFetchingCalls.Should().BeGreaterThan(0);
        f.Inner.Token!.Value.IsCancellationRequested.Should().BeTrue();
        //and it leaves the lock alone for the grace period so the other host gets there first
        await Task.Delay(200);
        f.Locks.Holder(Lock).Should().BeNull();
    }

    [Test]
    public async Task Should_name_the_host_when_it_takes_hands_over_and_lets_go_of_a_lock()
    {
        //arrange
        var log = new ListLogger();
        await using var f = new Fixture(log: log);
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Prefer(Lock, Other);
        await Eventually(() => f.Locks.Holder(Lock) == null);

        //assert
        log.Messages.Should()
            .Contain($"Starting Singleton Processor with name {Lock} on host {Me}")
            .And.Contain(
                $"Singleton Processor with name {Lock} on host {Me} hands its lock over to {Other}"
            );
        await Eventually(() =>
            log.Messages.Contains(
                $"Singleton Processor with name {Lock} on host {Me} stopped holding its lock"
            )
        );
    }

    [Test]
    public async Task Should_keep_the_lock_until_the_cancelled_message_has_stopped()
    {
        //arrange: a message that keeps running after it is cancelled
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        var message = f.InFlight.ProcessAsync(
            Mock.Of<IMessageStateHandler<TestCommand>>(),
            CancellationToken.None
        );
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Prefer(Lock, Other);
        await Eventually(() => f.Inner.Token!.Value.IsCancellationRequested);
        await Task.Delay(300);

        //assert: the receiver is stopped, but the lock stays until the message is done
        f.Locks.Holder(Lock).Should().Be(Me);
        f.Processor.Gate.SetResult();
        await message;
        await Eventually(() => f.Locks.Holder(Lock) == null);
    }

    [Test]
    public async Task Should_keep_the_lock_for_as_long_as_a_cancelled_message_keeps_running()
    {
        //arrange: a message that ignores its cancellation for longer than its message lock
        await using var f = new Fixture(lockTimeout: TimeSpan.FromMilliseconds(100));
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        _ = f.InFlight.ProcessAsync(
            Mock.Of<IMessageStateHandler<TestCommand>>(),
            CancellationToken.None
        );
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Prefer(Lock, Other);
        await Eventually(() => f.Inner.Token!.Value.IsCancellationRequested);
        await Task.Delay(500);

        //assert: the next holder must not start while the message is still running
        f.Locks.Holder(Lock).Should().Be(Me);
        f.Processor.Gate.SetResult();
        await Eventually(() => f.Locks.Holder(Lock) == null);
    }

    [Test]
    public async Task Should_finish_the_hand_over_when_stopping_the_receiver_fails()
    {
        //arrange: something registered on the receiver's token throws when it is cancelled
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        f.Inner.Token!.Value.Register(() => throw new InvalidOperationException("callback"));
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Prefer(Lock, Other);

        //assert: the receiver is stopped and the lock is still given up
        await Eventually(() => f.Locks.Holder(Lock) == null);
        f.Inner.Token!.Value.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public async Task Should_not_take_the_lock_again_while_the_last_message_is_still_running()
    {
        //arrange: a message is running when the lock is lost
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        var message = f.InFlight.ProcessAsync(
            Mock.Of<IMessageStateHandler<TestCommand>>(),
            CancellationToken.None
        );

        //act
        f.Locks.FailRenewals = true;
        await Eventually(() => f.Locks.Holder(Lock) == null);
        f.Locks.FailRenewals = false;
        await Task.Delay(300);

        //assert: a new receiver would run alongside the old message
        f.Locks.Holder(Lock).Should().BeNull();
        f.Processor.Gate.SetResult();
        await message;
        await Eventually(() => f.Locks.Holder(Lock) == Me);
    }

    [Test]
    public async Task Should_release_the_lock_and_try_again_when_the_receiver_fails_to_start()
    {
        //arrange: another host holds the lock, and this host's receiver cannot start
        var log = new ListLogger();
        await using var f = new Fixture(log: log);
        f.Locks.Take(Lock, Other);
        await f.Start();
        f.Inner.FailStart = true;

        //act
        f.Locks.ReleaseFor(Lock);
        await Eventually(() => f.Inner.Started >= 2);
        f.Inner.FailStart = false;

        //assert: the lock is not held without processing, and a failed start is not a lost lock
        await Eventually(() => f.Locks.Holder(Lock) == Me && f.Inner.Started >= 3);
        log.Messages.Should().NotContain(m => m.Contains("lost its lock"));
    }

    [Test]
    public async Task Should_not_hand_over_a_lock_it_has_only_just_taken()
    {
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        f.Time.Advance(TimeSpan.FromMinutes(1));

        f.Placement.Prefer(Lock, Other);
        await Task.Delay(300);

        f.Locks.Holder(Lock).Should().Be(Me);
    }

    [Test]
    public async Task Should_let_the_running_message_finish_before_cancelling_it()
    {
        //arrange
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        f.Inner.HoldDrain = new TaskCompletionSource();
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Prefer(Lock, Other);
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
    public async Task Should_cancel_a_message_that_outlasts_its_message_lock()
    {
        //arrange
        await using var f = new Fixture(lockTimeout: TimeSpan.FromMilliseconds(150));
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        f.Inner.HoldDrain = new TaskCompletionSource(); //never completes
        f.Time.Advance(TimeSpan.FromMinutes(11));

        //act
        f.Placement.Prefer(Lock, Other);

        //assert
        await Eventually(() => f.Locks.Holder(Lock) == null);
        f.Inner.Token!.Value.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public async Task Should_release_the_lock_when_the_host_stops_during_a_hand_over()
    {
        //arrange: a hand-over is waiting for a message that takes a long time
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        f.Inner.HoldDrain = new TaskCompletionSource(); //never completes
        f.Time.Advance(TimeSpan.FromMinutes(11));
        f.Placement.Prefer(Lock, Other);
        await Eventually(() => f.Inner.StopFetchingCalls == 1);
        f.Locks.Holder(Lock).Should().Be(Me);

        //act
        f.Shutdown.Cancel();

        //assert: stopping the host does not wait for the drain, the lock is given up
        await Eventually(() => f.Locks.Holder(Lock) == null);
    }

    [Test]
    public async Task Should_cancel_and_release_when_the_receiver_cannot_drain()
    {
        await using var f = new Fixture(drainable: false);
        f.Placement.Prefer(Lock, Me);
        await f.Start();
        f.Time.Advance(TimeSpan.FromMinutes(11));

        f.Placement.Prefer(Lock, Other);

        await Eventually(() => f.Locks.Holder(Lock) == null);
        f.Inner.StopFetchingCalls.Should().Be(0);
        f.Inner.Token!.Value.IsCancellationRequested.Should().BeTrue();
    }

    [Test]
    public async Task Should_keep_the_lock_when_the_advice_goes_away()
    {
        //arrange: the placement lost sight of the group while this host holds the lock
        await using var f = new Fixture();
        f.Placement.Prefer(Lock, Me);
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

    private sealed class FakePlacement : ISingletonPlacement
    {
        private readonly ConcurrentDictionary<string, string> _advice = new();

        public string HostId => Me;

        public void Prefer(string lockId, string host) => _advice[lockId] = host;

        public void Clear(string lockId) => _advice.TryRemove(lockId, out _);

        public Task StartAsync(
            IReadOnlyDictionary<string, int> locks,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public string? Advise(string lockId) =>
            _advice.TryGetValue(lockId, out var advice) ? advice : null;
    }

    private sealed class InMemoryLocks : ISingletonLockManager, ISingletonLockInspector
    {
        private readonly ConcurrentDictionary<string, string> _holders = new();
        private int _attempts;
        private int _failuresLeft;
        public int Attempts => _attempts;
        private int _inspections;
        public int Inspections => _inspections;

        //The next attempts throw, like a storage error that outlasts the SDK's retries
        public void FailNext(int attempts) => Volatile.Write(ref _failuresLeft, attempts);

        //Renewals fail from now on, like a lease that another host has broken
        public bool FailRenewals { get; set; }

        public void Take(string lockId, string host) => _holders[lockId] = host;

        public void ReleaseFor(string lockId) => _holders.TryRemove(lockId, out _);

        public string? Holder(string lockId) =>
            _holders.TryGetValue(lockId, out var holder) ? holder : null;

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<bool> IsLockedAsync(string lockId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _inspections);
            return Task.FromResult(_holders.ContainsKey(lockId));
        }

        public Task<ISingletonLockHandle?> TryLockAsync(
            string lockId,
            TimeSpan lockPeriod,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref _attempts);
            if (Interlocked.Decrement(ref _failuresLeft) >= 0)
                throw new InvalidOperationException("storage unavailable");
            return Task.FromResult<ISingletonLockHandle?>(
                _holders.TryAdd(lockId, Me) ? new Handle(this, lockId) : null
            );
        }

        private sealed class Handle(InMemoryLocks owner, string lockId) : ISingletonLockHandle
        {
            public string LeaseId => Me;
            public string LockId => lockId;

            public Task<bool> RenewAsync(ILogger log, CancellationToken cancellationToken) =>
                owner.FailRenewals
                    ? throw new InvalidOperationException("lease lost")
                    : Task.FromResult(true);

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

        //Starting throws while this is set
        public volatile bool FailStart;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _started);
            Token = cancellationToken;
            if (FailStart)
                throw new InvalidOperationException("start");
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

    private sealed class ListLogger : ILogger
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Messages.Enqueue(formatter(state, exception));
    }

    //Handles a message until the gate opens, whatever its cancellation token says
    private sealed class GatedProcessor : IMessageProcessor
    {
        public TaskCompletionSource Gate { get; } = new();

        public Task ProcessAsync<T>(
            IMessageStateHandler<T> messageStateHandler,
            CancellationToken cancellationToken
        )
            where T : class, IMessage => Gate.Task;
    }
}
