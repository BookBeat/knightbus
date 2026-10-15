using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.Singleton;
using KnightBus.Host.Singleton;
using KnightBus.Host.Tests.Unit.ExampleProcessors;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

[TestFixture]
public class KnightBusHostPlacementTests
{
    private const string Me = "me";

    private sealed class Fixture
    {
        public readonly Mock<ISingletonMembership> Membership = new();
        public readonly Mock<ISingletonLockManager> Locks = new();
        public readonly KnightBusHost Host;

        public Fixture(IPlugin? plugin = null)
        {
            Membership.Setup(x => x.HostId).Returns(Me);
            Membership
                .Setup(x => x.GetMembersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            //One singleton processor, so the host has a lock to place
            var receiver = new Mock<IChannelReceiver>();
            receiver.Setup(x => x.Settings).Returns(new SingletonProcessingSettings());
            var channelFactory = new Mock<ITransportChannelFactory>();
            channelFactory.Setup(x => x.Configuration).Returns(Mock.Of<ITransportConfiguration>());
            channelFactory.Setup(x => x.CanCreate(typeof(SingletonCommand))).Returns(true);
            channelFactory
                .Setup(x =>
                    x.Create(
                        typeof(SingletonCommand),
                        null,
                        It.IsAny<IProcessingSettings>(),
                        It.IsAny<IMessageSerializer>(),
                        It.IsAny<IHostConfiguration>(),
                        It.IsAny<IMessageProcessor>()
                    )
                )
                .Returns(receiver.Object);
            var transport = new Mock<ITransport>();
            transport.Setup(x => x.TransportChannelFactories).Returns([channelFactory.Object]);

            var services = new ServiceCollection()
                .AddSingleton(Membership.Object)
                .AddSingleton(transport.Object);
            services.RegisterProcessor<SingletonCommandProcessor>();
            services.UseSingletonLocks(Locks.Object);
            if (plugin != null)
                services.AddSingleton(plugin);
            Host = new KnightBusHost(
                new HostConfiguration(),
                services.BuildServiceProvider(),
                Mock.Of<ILogger<KnightBusHost>>()
            );
        }

        public void VerifyLeft() =>
            Membership.Verify(x => x.LeaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Should_join_with_the_singleton_locks_and_give_the_receivers_its_advice()
    {
        //arrange: another host is the preferred holder of the only lock
        var f = new Fixture();
        f.Membership.Setup(x =>
                x.JoinAsync(
                    It.IsAny<IReadOnlyDictionary<string, int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (IReadOnlyDictionary<string, int> locks, CancellationToken _) =>
                    f
                        .Membership.Setup(x => x.GetMembersAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync([new SingletonMember(PreferredOver(Me, locks), locks)])
            );

        //act
        await f.Host.StartAsync(CancellationToken.None);

        //assert: the receiver leaves the lock to the preferred host, which it only knows through
        //the placement the host joined with
        f.Membership.Verify(x =>
            x.JoinAsync(
                It.Is<IReadOnlyDictionary<string, int>>(locks => locks.Count == 1),
                It.IsAny<CancellationToken>()
            )
        );
        f.Locks.Verify(
            x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );

        await f.Host.StopAsync(CancellationToken.None);
        f.VerifyLeft();
    }

    [Test]
    public async Task Should_leave_the_placement_when_starting_fails()
    {
        //arrange: something after the placement fails to start
        var plugin = new Mock<IPlugin>();
        plugin
            .Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("plugin failed"));
        var f = new Fixture(plugin.Object);

        //act
        var start = () => f.Host.StartAsync(CancellationToken.None);

        //assert: the others must not keep counting on a host that never started
        await start.Should().ThrowAsync<InvalidOperationException>();
        f.VerifyLeft();
    }

    [Test]
    public async Task Should_start_without_placement_when_joining_it_times_out()
    {
        //arrange: an operation that timed out reports the same exception type as a cancelled one
        var f = new Fixture();
        f.Membership.Setup(x =>
                x.JoinAsync(
                    It.IsAny<IReadOnlyDictionary<string, int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new TaskCanceledException("timeout"));

        //act
        var start = () => f.Host.StartAsync(CancellationToken.None);

        //assert
        await start.Should().NotThrowAsync();
        await f.Host.StopAsync(CancellationToken.None);
        f.VerifyLeft();
    }

    [Test]
    public async Task Should_leave_the_placement_when_the_start_is_cancelled_after_joining()
    {
        //arrange
        using var cts = new CancellationTokenSource();
        var f = new Fixture();
        f.Membership.Setup(x => x.GetMembersAsync(It.IsAny<CancellationToken>()))
            .Returns(
                async (CancellationToken token) =>
                {
                    await cts.CancelAsync();
                    token.ThrowIfCancellationRequested();
                    return [];
                }
            );

        //act
        var start = () => f.Host.StartAsync(cts.Token);

        //assert
        await start.Should().ThrowAsync<OperationCanceledException>();
        f.VerifyLeft();
    }

    //A host id that the assignment prefers over this host for every lock
    private static string PreferredOver(string self, IReadOnlyDictionary<string, int> locks) =>
        Enumerable
            .Range(0, 100)
            .Select(i => $"other-{i}")
            .First(other =>
                SingletonAssignment
                    .Compute([new SingletonMember(self, locks), new SingletonMember(other, locks)])
                    .Values.All(holder => holder == other)
            );
}
