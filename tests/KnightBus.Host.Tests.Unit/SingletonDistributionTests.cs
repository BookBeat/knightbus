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
/// How singleton locks are distributed between hosts without placement: every lock is an
/// independent race, so the first host to start takes every lock and keeps it.
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
        var storage = new InMemoryLockStore();
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
                await receiver.StartAsync(cts.Token, cts.Token);
            }
        }

        //assert: the first host owns everything, the others own nothing
        var locksPerHost = storage.HoldersByHost();
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
        var storage = new InMemoryLockStore();
        using var cts = new CancellationTokenSource();
        var first = new InMemoryLockManager(storage, "host-0");
        var second = new InMemoryLockManager(storage, "host-1");
        var firstReceiver = CreateReceiver(first, "Processor");
        var secondReceiver = CreateReceiver(second, "Processor", TimeSpan.FromMilliseconds(50));

        //act: let the waiting host poll many times while the holder keeps renewing
        await firstReceiver.StartAsync(cts.Token, cts.Token);
        await secondReceiver.StartAsync(cts.Token, cts.Token);
        //a loaded machine can stall the polling, so wait for the attempts instead of a fixed time
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (second.FailedAttempts <= 5 && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        //assert: no matter how long the second host waits, it never gets the lock
        second.FailedAttempts.Should().BeGreaterThan(5);
        storage.Holder("Processor").Should().Be("host-0");

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
        return new SingletonChannelReceiver(
            underlying.Object,
            new InFlightMessages(Mock.Of<IMessageProcessor>()),
            manager,
            Mock.Of<ILogger>(),
            lockId
        )
        {
            TimerInterval = timerInterval ?? TimeSpan.FromMinutes(1),
            LockRefreshInterval = TimeSpan.FromMilliseconds(100),
        };
    }
}
