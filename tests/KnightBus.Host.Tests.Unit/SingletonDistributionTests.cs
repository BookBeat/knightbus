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
/// Characterizes how singleton locks are distributed between hosts. These tests document the
/// current behaviour (the first host to start takes every lock); when a distribution mechanism
/// is introduced the assertions are expected to change.
/// </summary>
[TestFixture]
public class SingletonDistributionTests
{
    private const int LockCount = 16;
    private const int HostCount = 4;

    [Test]
    public async Task First_host_to_start_takes_every_lock()
    {
        //arrange: one shared "storage account", HostCount hosts each running LockCount singleton processors
        var storage = new SharedLockStorage();
        var receivers = new List<(int Host, SingletonChannelReceiver Receiver)>();

        //act: hosts start one after another, and each host starts its receivers in sequence,
        //like KnightBusHost.StartAsync does
        using var cts = new CancellationTokenSource();
        for (var host = 0; host < HostCount; host++)
        {
            var manager = new InMemoryLockManager(storage, $"host-{host}");
            for (var i = 0; i < LockCount; i++)
            {
                var receiver = CreateReceiver(manager, $"Processor{i}");
                receivers.Add((host, receiver));
                await receiver.StartAsync(cts.Token);
            }
        }

        //assert: the first host owns everything, the others own nothing
        var locksPerHost = storage
            .Holders.Values.GroupBy(h => h)
            .ToDictionary(g => g.Key, g => g.Count());
        TestContext.Out.WriteLine(
            "Locks per host: "
                + string.Join(
                    ", ",
                    locksPerHost.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}")
                )
        );
        locksPerHost.Should().ContainSingle().Which.Key.Should().Be("host-0");
        locksPerHost["host-0"].Should().Be(LockCount);

        cts.Cancel();
    }

    [Test]
    public async Task Waiting_hosts_do_not_take_over_a_healthy_holder()
    {
        //arrange
        var storage = new SharedLockStorage();
        using var cts = new CancellationTokenSource();
        var first = new InMemoryLockManager(storage, "host-0");
        var second = new InMemoryLockManager(storage, "host-1");
        var firstReceiver = CreateReceiver(first, "Processor");
        var secondReceiver = CreateReceiver(second, "Processor", TimeSpan.FromMilliseconds(50));

        //act: let the waiting host poll many times while the holder keeps renewing
        await firstReceiver.StartAsync(cts.Token);
        await secondReceiver.StartAsync(cts.Token);
        await Task.Delay(1000);

        //assert: no matter how long the second host waits, it never gets the lock
        storage.Holders["Processor"].Should().Be("host-0");
        second.FailedAttempts.Should().BeGreaterThan(5);

        cts.Cancel();
    }

    private static SingletonChannelReceiver CreateReceiver(
        ISingletonLockManager manager,
        string lockId,
        TimeSpan? timerInterval = null
    )
    {
        var underlying = new Mock<IChannelReceiver>();
        underlying.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
        return new SingletonChannelReceiver(underlying.Object, manager, Mock.Of<ILogger>(), lockId)
        {
            TimerInterval = timerInterval ?? TimeSpan.FromMinutes(1),
            LockRefreshInterval = TimeSpan.FromMilliseconds(100),
        };
    }

    private class SharedLockStorage
    {
        public ConcurrentDictionary<string, string> Holders { get; } = new();
    }

    /// <summary>A blob-lease stand-in: an uncontended lock is granted to whoever asks first.</summary>
    private class InMemoryLockManager(SharedLockStorage storage, string hostId)
        : ISingletonLockManager
    {
        private int _failedAttempts;
        public int FailedAttempts => _failedAttempts;

        public Task InitializeAsync() => Task.CompletedTask;

        public Task<ISingletonLockHandle?> TryLockAsync(
            string lockId,
            TimeSpan lockPeriod,
            CancellationToken cancellationToken
        )
        {
            if (storage.Holders.TryAdd(lockId, hostId))
                return Task.FromResult<ISingletonLockHandle?>(new Handle(storage, lockId, hostId));

            Interlocked.Increment(ref _failedAttempts);
            return Task.FromResult<ISingletonLockHandle?>(null);
        }
    }

    private class Handle(SharedLockStorage storage, string lockId, string hostId)
        : ISingletonLockHandle
    {
        public string LeaseId => hostId;
        public string LockId => lockId;

        public Task<bool> RenewAsync(ILogger log, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            storage.Holders.TryRemove(new KeyValuePair<string, string>(lockId, hostId));
            return Task.CompletedTask;
        }
    }
}
