using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

[TestFixture]
public class KnightBusHostPlacementTests
{
    //The host only joins the placement when it has a transport
    private static KnightBusHost CreateHost(ISingletonPlacement placement, IPlugin? plugin = null)
    {
        var transport = new Mock<ITransport>();
        transport.Setup(x => x.TransportChannelFactories).Returns([]);
        var services = new ServiceCollection()
            .AddSingleton(placement)
            .AddSingleton(transport.Object);
        if (plugin != null)
            services.AddSingleton(plugin);
        return new KnightBusHost(
            new HostConfiguration(),
            services.BuildServiceProvider(),
            Mock.Of<ILogger<KnightBusHost>>()
        );
    }

    [Test]
    public async Task Should_leave_the_placement_when_starting_fails()
    {
        //arrange: something after the placement fails to start
        var placement = new Mock<ISingletonPlacement>();
        var plugin = new Mock<IPlugin>();
        plugin
            .Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("plugin failed"));
        var host = CreateHost(placement.Object, plugin.Object);

        //act
        var start = () => host.StartAsync(CancellationToken.None);

        //assert: the others must not keep counting on a host that never started
        await start.Should().ThrowAsync<InvalidOperationException>();
        placement.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Should_start_without_placement_when_joining_it_times_out()
    {
        //arrange: an operation that timed out reports the same exception type as a cancelled one
        var placement = new Mock<ISingletonPlacement>();
        placement
            .Setup(x =>
                x.StartAsync(
                    It.IsAny<IReadOnlyDictionary<string, int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new TaskCanceledException("timeout"));
        var host = CreateHost(placement.Object);

        //act
        var start = () => host.StartAsync(CancellationToken.None);

        //assert
        await start.Should().NotThrowAsync();
        placement.Verify(
            x =>
                x.StartAsync(
                    It.IsAny<IReadOnlyDictionary<string, int>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        await host.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_stop_starting_when_the_start_is_cancelled_while_joining()
    {
        //arrange
        using var cts = new CancellationTokenSource();
        var placement = new Mock<ISingletonPlacement>();
        placement
            .Setup(x =>
                x.StartAsync(
                    It.IsAny<IReadOnlyDictionary<string, int>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (IReadOnlyDictionary<string, int> _, CancellationToken token) =>
                {
                    await cts.CancelAsync();
                    token.ThrowIfCancellationRequested();
                }
            );
        var host = CreateHost(placement.Object);

        //act
        var start = () => host.StartAsync(cts.Token);

        //assert
        await start.Should().ThrowAsync<OperationCanceledException>();
    }
}
