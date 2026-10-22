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
using KnightBus.Tests.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

/// <summary>
/// Several hosts with real receivers share one lock store and one view of the group, and the
/// preferred holder comes from the real <see cref="SingletonAssignment"/>. Checks the thing the
/// feature is for: locks that piled up on the first host spread out when others join, and a host
/// that leaves has its locks taken over. Every start of a receiver runs one message, so a lock
/// that moves before its message has stopped shows up as work running on two hosts.
/// </summary>
[TestFixture]
public class SingletonRebalanceSimulationTests
{
    private static readonly string[] LockIds = Enumerable
        .Range(0, 16)
        .Select(i => $"Lock{i:D2}")
        .ToArray();

    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(20);

    [Test]
    public async Task Should_spread_the_locks_when_hosts_join_and_take_them_over_when_one_leaves()
    {
        var world = new World();

        //the first host starts alone and, as always, takes everything
        var first = await world.StartHost("host-0");
        world.Locks.HoldersByHost().Should().ContainSingle().Which.Key.Should().Be("host-0");
        world.Locks.HoldersByHost()["host-0"].Should().Be(LockIds.Length);

        //three more join, and time passes until the first host has held its locks long enough
        var others = new List<Host>
        {
            await world.StartHost("host-1"),
            await world.StartHost("host-2"),
            await world.StartHost("host-3"),
        };
        world.Time.Advance(TimeSpan.FromMinutes(11));
        await Eventually.True(() => world.HoldsWhatIsPreferred(), Settle, world.Describe);

        var spread = world.Locks.HoldersByHost();
        TestContext.Out.WriteLine(
            "After join: "
                + string.Join(", ", spread.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"))
        );
        spread.Keys.Should().BeEquivalentTo(["host-0", "host-1", "host-2", "host-3"]);
        spread.Values.Should().OnlyContain(v => v >= 2 && v <= 5, "16 locks on 4 hosts");

        //one host leaves, like a pod being replaced: its locks are released and the others take them over
        await others[0].Leave();
        //its locks go to the preferred hosts at once. The time must not jump while one is still free,
        //or a host that is not preferred has seen it free for the whole grace period and takes it
        await Eventually.True(
            () => world.Locks.HoldersByHost().Values.Sum() == LockIds.Length,
            Settle,
            world.Describe
        );
        //locks that had to move between the remaining hosts wait out the hand-over interval so that
        //nothing moves back and forth
        world.Time.Advance(TimeSpan.FromMinutes(11));
        await Eventually.True(() => world.HoldsWhatIsPreferred(), Settle, world.Describe);

        var after = world.Locks.HoldersByHost();
        TestContext.Out.WriteLine(
            "After leave: "
                + string.Join(", ", after.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"))
        );
        after.Keys.Should().NotContain("host-1");
        after.Values.Sum().Should().Be(LockIds.Length, "every lock is held by someone");
        after.Values.Should().OnlyContain(v => v >= 3 && v <= 7, "16 locks on 3 hosts");

        await first.Leave();
        foreach (var host in others.Skip(1))
            await host.Leave();
        world.MaxConcurrentWork.Should().Be(1, "a lock's work never runs on two hosts at once");
    }

    [Test]
    public async Task Should_leave_the_locks_where_they_are_while_the_hosts_stay_the_same()
    {
        //arrange: four hosts settle on the preferred distribution
        var world = new World();
        var hosts = new List<Host>();
        for (var i = 0; i < 4; i++)
            hosts.Add(await world.StartHost($"host-{i}"));
        world.Time.Advance(TimeSpan.FromMinutes(11));
        await Eventually.True(() => world.HoldsWhatIsPreferred(), Settle, world.Describe);
        var settled = world.Locks.Holders();
        var takes = world.Locks.Takes;

        //act: an hour passes, well past the minimum hold, while every receiver keeps polling
        for (var i = 0; i < 6; i++)
        {
            world.Time.Advance(TimeSpan.FromMinutes(11));
            await Task.Delay(200);
        }

        //assert: the minimum hold is not a period, a lock on its preferred host never moves
        world.Locks.Holders().Should().BeEquivalentTo(settled);
        world.Locks.Takes.Should().Be(takes);

        foreach (var host in hosts)
            await host.Leave();
        world.MaxConcurrentWork.Should().Be(1, "a lock's work never runs on two hosts at once");
    }

    private sealed class World
    {
        public readonly ManualTime Time = new();
        public readonly InMemoryLockStore Locks = new();
        private readonly ConcurrentDictionary<string, long> _members = new();
        private readonly ConcurrentDictionary<string, int> _work = new();
        private int _maxConcurrentWork;
        public int MaxConcurrentWork => Volatile.Read(ref _maxConcurrentWork);

        public async Task<Host> StartHost(string id)
        {
            _members[id] = Time.GetTimestamp();
            var host = new Host(this, id);
            await host.Start();
            return host;
        }

        public void Remove(string id) => _members.TryRemove(id, out _);

        public Dictionary<string, string> Preferred() =>
            SingletonAssignment
                .Compute(
                    _members
                        .Keys.Select(m => new SingletonMember(
                            m,
                            LockIds.ToDictionary(l => l, _ => 1)
                        ))
                        .ToList()
                )
                .ToDictionary(x => x.Key, x => x.Value);

        public string Describe()
        {
            var preferred = Preferred();
            return string.Join(
                "; ",
                LockIds
                    .Where(l => Locks.Holder(l) != preferred[l])
                    .Select(l =>
                        $"{l} held by {Locks.Holder(l) ?? "nobody"}, preferred {preferred[l]}"
                    )
            );
        }

        public bool HoldsWhatIsPreferred()
        {
            var preferred = Preferred();
            return LockIds.All(l => Locks.Holder(l) == preferred[l]);
        }

        public string? Advise(string self, string lockId)
        {
            if (!_members.ContainsKey(self))
                return null;
            return Preferred()[lockId];
        }

        public void StartWork(string lockId)
        {
            var current = _work.AddOrUpdate(lockId, 1, (_, count) => count + 1);
            int seen;
            while (
                (seen = _maxConcurrentWork) < current
                && Interlocked.CompareExchange(ref _maxConcurrentWork, current, seen) != seen
            ) { }
        }

        public void EndWork(string lockId) => _work.AddOrUpdate(lockId, 0, (_, count) => count - 1);
    }

    private sealed class Host
    {
        private readonly World _world;
        private readonly string _id;
        private readonly CancellationTokenSource _shutdown = new();
        private readonly CancellationTokenSource _teardown = new();
        private readonly List<SingletonChannelReceiver> _receivers = new();
        private readonly List<InFlightMessages> _inFlight = new();

        public Host(World world, string id)
        {
            _world = world;
            _id = id;
        }

        public async Task Start()
        {
            foreach (var lockId in LockIds)
            {
                var inFlight = new InFlightMessages(new Work(_world, lockId));
                _inFlight.Add(inFlight);
                var receiver = new SingletonChannelReceiver(
                    new Inner(inFlight),
                    inFlight,
                    new InMemoryLockManager(_world.Locks, _id),
                    Mock.Of<ILogger>(),
                    lockId,
                    _teardown.Token,
                    new Placement(_world, _id)
                )
                {
                    TimerInterval = TimeSpan.FromMilliseconds(20),
                    LockRefreshInterval = TimeSpan.FromMilliseconds(20),
                    Time = _world.Time,
                };
                _receivers.Add(receiver);
                await receiver.StartAsync(_shutdown.Token, _shutdown.Token);
            }
        }

        //Stops the way the host does: the locks are kept until the running messages have stopped
        public async Task Leave()
        {
            _world.Remove(_id);
            await _shutdown.CancelAsync();
            await Task.WhenAll(_inFlight.Select(f => f.WaitUntilIdleAsync(CancellationToken.None)));
            await _teardown.CancelAsync();
            await Task.WhenAll(_receivers.Select(r => r.TeardownCompletion));
        }
    }

    private sealed class Placement(World world, string self) : ISingletonPlacement
    {
        public string HostId => self;

        public Task StartAsync(
            IReadOnlyDictionary<string, int> locks,
            CancellationToken cancellationToken
        ) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public string? Advise(string lockId) => world.Advise(self, lockId);
    }

    //Hands one message to the pipeline on every start, which runs until fetching stops
    private sealed class Inner(InFlightMessages inFlight) : IChannelReceiver
    {
        public IProcessingSettings Settings { get; set; } =
            new SingletonProcessingSettings { MessageLockTimeout = TimeSpan.FromSeconds(5) };

        public Task StartAsync(CancellationToken stopFetching, CancellationToken cancellationToken)
        {
            var running = CancellationTokenSource.CreateLinkedTokenSource(
                stopFetching,
                cancellationToken
            );
            _ = inFlight
                .ProcessAsync(Mock.Of<IMessageStateHandler<TestCommand>>(), running.Token)
                .ContinueWith(_ => running.Dispose(), TaskScheduler.Default);
            return Task.CompletedTask;
        }
    }

    //Takes a while to wrap up once told to stop, so another host that starts too early overlaps it
    private sealed class Work(World world, string lockId) : IMessageProcessor
    {
        public async Task ProcessAsync<T>(
            IMessageStateHandler<T> messageStateHandler,
            CancellationToken cancellationToken
        )
            where T : class, IMessage
        {
            world.StartWork(lockId);
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken)
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                await Task.Delay(50, CancellationToken.None);
            }
            finally
            {
                world.EndWork(lockId);
            }
        }
    }
}
