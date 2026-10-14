using System;
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
    private static KnightBusHost CreateHost(ISingletonPlacement placement)
    {
        var transport = new Mock<ITransport>();
        transport.Setup(x => x.TransportChannelFactories).Returns([]);
        return new KnightBusHost(
            new HostConfiguration(),
            new ServiceCollection()
                .AddSingleton(placement)
                .AddSingleton(transport.Object)
                .BuildServiceProvider(),
            Mock.Of<ILogger<KnightBusHost>>()
        );
    }

    [Test]
    public async Task Should_start_without_placement_when_joining_it_times_out()
    {
        //arrange: an operation that timed out reports the same exception type as a cancelled one
        var placement = new Mock<ISingletonPlacement>();
        placement
            .Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("timeout"));
        var host = CreateHost(placement.Object);

        //act
        var start = () => host.StartAsync(CancellationToken.None);

        //assert
        await start.Should().NotThrowAsync();
        placement.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        await host.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Should_stop_starting_when_the_start_is_cancelled_while_joining()
    {
        //arrange
        using var cts = new CancellationTokenSource();
        var placement = new Mock<ISingletonPlacement>();
        placement
            .Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(
                async (CancellationToken token) =>
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
