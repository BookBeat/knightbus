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
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

/// <summary>
/// Several hosts with real receivers share one lock store and one view of the group, and the
/// preferred holder comes from the real <see cref="SingletonAssignment"/>. Checks the thing the
/// feature is for: locks that piled up on the first host spread out when others join, and a host
/// that leaves has its locks taken over.
/// </summary>
[TestFixture]
public class SingletonRebalanceSimulationTests
{
    private static readonly string[] LockIds = Enumerable
        .Range(0, 16)
        .Select(i => $"Lock{i:D2}")
        .ToArray();

    private static readonly SingletonPlacementOptions Options = new()
    {
        StabilityWindow = TimeSpan.FromMinutes(2),
        TakeoverGrace = TimeSpan.FromMinutes(2),
        HandoffInterval = TimeSpan.FromMinutes(10),
        DrainTimeout = TimeSpan.FromSeconds(5),
    };

    [Test]
    public async Task Should_spread_the_locks_when_hosts_join_and_take_them_over_when_one_leaves()
    {
        var world = new World();

        //the first host starts alone and, as always, takes everything
        var first = await world.StartHost("host-0");
        world.HoldersByHost().Should().ContainSingle().Which.Key.Should().Be("host-0");
        world.HoldersByHost()["host-0"].Should().Be(LockIds.Length);

        //three more join, time passes until they are settled and the first host has held its locks long enough
        var others = new List<Host>
        {
            await world.StartHost("host-1"),
            await world.StartHost("host-2"),
            await world.StartHost("host-3"),
        };
        world.Time.Advance(TimeSpan.FromMinutes(11));
        await Eventually(() => world.HoldsWhatIsPreferred(), world);

        var spread = world.HoldersByHost();
        TestContext.Out.WriteLine(
            "After join: "
                + string.Join(", ", spread.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"))
        );
        spread.Keys.Should().BeEquivalentTo(["host-0", "host-1", "host-2", "host-3"]);
        spread.Values.Should().OnlyContain(v => v >= 2 && v <= 5, "16 locks on 4 hosts");

        //one host leaves, like a pod being replaced: its locks are released and the others take them over
        await others[0].Leave();
        //its locks go to the preferred hosts at once; locks that had to move between the remaining hosts
        //wait out the hand-over interval so that nothing moves back and forth
        world.Time.Advance(TimeSpan.FromMinutes(11));
        await Eventually(() => world.HoldsWhatIsPreferred(), world);

        var after = world.HoldersByHost();
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
        world.MaxConcurrentHolders.Should().Be(1, "a lock is never held by two hosts at once");
    }

    private static async Task Eventually(Func<bool> condition, World world)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Timed out waiting for the locks to settle: " + world.Describe());
            await Task.Delay(20);
        }
    }

    private sealed class World
    {
        public readonly ManualTime Time = new();
        private readonly ConcurrentDictionary<string, string> _holders = new();
        private readonly ConcurrentDictionary<string, long> _members = new();
        private int _maxConcurrentHolders = 1;
        public int MaxConcurrentHolders => _maxConcurrentHolders;

        public async Task<Host> StartHost(string id)
        {
            _members[id] = Time.GetTimestamp();
            var host = new Host(this, id);
            await host.Start();
            return host;
        }

        public void Remove(string id) => _members.TryRemove(id, out _);

        public Dictionary<string, int> HoldersByHost() =>
            _holders.Values.GroupBy(h => h).ToDictionary(g => g.Key, g => g.Count());

        public Dictionary<string, string> Preferred() =>
            SingletonAssignment
                .Compute(
                    _members
                        .Keys.Select(m => new SingletonHostInfo(
                            m,
                            LockIds.ToDictionary(l => l, _ => 1)
                        ))
                        .ToList(),
                    Options.Epsilon
                )
                .ToDictionary(x => x.Key, x => x.Value);

        public string Describe()
        {
            var preferred = Preferred();
            return string.Join(
                "; ",
                LockIds
                    .Where(l => !_holders.TryGetValue(l, out var h) || h != preferred[l])
                    .Select(l =>
                        $"{l} held by {(_holders.TryGetValue(l, out var h) ? h : "nobody")}, preferred {preferred[l]}"
                    )
            );
        }

        public bool HoldsWhatIsPreferred()
        {
            var preferred = Preferred();
            return LockIds.All(l =>
                _holders.TryGetValue(l, out var holder) && holder == preferred[l]
            );
        }

        public SingletonPlacementAdvice? Advise(string self, string lockId)
        {
            if (!_members.ContainsKey(self))
                return null;
            var preferred = Preferred()[lockId];
            return new SingletonPlacementAdvice(
                preferred,
                preferred == self,
                Time.GetElapsedTime(_members[preferred])
            );
        }

        public bool IsHeld(string lockId) => _holders.ContainsKey(lockId);

        public bool TryTake(string lockId, string host)
        {
            var taken = _holders.TryAdd(lockId, host);
            if (taken)
            {
                var current = _holders.Count(x => x.Key == lockId);
                int seen;
                while (
                    (seen = _maxConcurrentHolders) < current
                    && Interlocked.CompareExchange(ref _maxConcurrentHolders, current, seen) != seen
                ) { }
            }
            return taken;
        }

        public void Release(string lockId, string host) =>
            _holders.TryRemove(new KeyValuePair<string, string>(lockId, host));
    }

    private sealed class Host
    {
        private readonly World _world;
        private readonly string _id;
        private readonly CancellationTokenSource _shutdown = new();
        private readonly List<SingletonChannelReceiver> _receivers = new();

        public Host(World world, string id)
        {
            _world = world;
            _id = id;
        }

        public async Task Start()
        {
            foreach (var lockId in LockIds)
            {
                var inner = new Inner();
                var receiver = new SingletonChannelReceiver(
                    inner,
                    new Locks(_world, _id),
                    Mock.Of<ILogger>(),
                    lockId,
                    null,
                    new Placement(_world, _id),
                    Options
                )
                {
                    TimerInterval = TimeSpan.FromMilliseconds(20),
                    LockRefreshInterval = TimeSpan.FromMilliseconds(20),
                    Time = _world.Time,
                };
                _receivers.Add(receiver);
                await receiver.StartAsync(_shutdown.Token);
            }
        }

        public async Task Leave()
        {
            _world.Remove(_id);
            await _shutdown.CancelAsync();
            await Task.WhenAll(_receivers.Select(r => r.TeardownCompletion));
        }
    }

    private sealed class Placement(World world, string self) : ISingletonPlacement
    {
        public string HostId => self;

        public void Register(string lockId, int weight) { }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public SingletonPlacementAdvice? Advise(string lockId) => world.Advise(self, lockId);
    }

    private sealed class Locks(World world, string host)
        : ISingletonLockManager,
            ISingletonLockInspector
    {
        public Task InitializeAsync() => Task.CompletedTask;

        public Task<bool> IsHeldAsync(string lockId, CancellationToken cancellationToken) =>
            Task.FromResult(world.IsHeld(lockId));

        public Task<ISingletonLockHandle?> TryLockAsync(
            string lockId,
            TimeSpan lockPeriod,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult<ISingletonLockHandle?>(
                world.TryTake(lockId, host) ? new Handle(world, lockId, host) : null
            );
    }

    private sealed class Handle(World world, string lockId, string host) : ISingletonLockHandle
    {
        public string LeaseId => host;
        public string LockId => lockId;

        public Task<bool> RenewAsync(ILogger log, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            world.Release(lockId, host);
            return Task.CompletedTask;
        }
    }

    private sealed class Inner : IDrainableChannelReceiver
    {
        public IProcessingSettings Settings { get; set; } = new SingletonProcessingSettings();

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopFetchingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }
}
