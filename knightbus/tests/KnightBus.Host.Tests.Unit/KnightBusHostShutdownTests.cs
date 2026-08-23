using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

[TestFixture]
public class KnightBusHostShutdownTests
{
    private static KnightBusHost CreateHost(IHostConfiguration configuration)
    {
        return new KnightBusHost(
            configuration,
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<ILogger<KnightBusHost>>()
        );
    }

    [Test]
    public async Task Should_stop_quickly_when_no_messages_are_in_flight()
    {
        //arrange: default grace period is 30 seconds
        var host = CreateHost(new HostConfiguration());
        await host.StartAsync(CancellationToken.None);

        //act
        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();

        //assert
        stopWatch
            .Elapsed.Should()
            .BeLessThan(
                TimeSpan.FromSeconds(5),
                "an idle host must not wait out the full shutdown grace period"
            );
    }

    [Test]
    public async Task Should_wait_for_in_flight_messages_before_stopping()
    {
        //arrange: hold one message in flight through the tracker
        var host = CreateHost(new HostConfiguration());
        var gate = new TaskCompletionSource();
        var nextProcessor = new Mock<IMessageProcessor>();
        nextProcessor
            .Setup(x =>
                x.ProcessAsync(
                    It.IsAny<IMessageStateHandler<TestCommand>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() => gate.Task);
        var processing = host.InFlightTracker.ProcessAsync(
            Mock.Of<IMessageStateHandler<TestCommand>>(),
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );

        //act: finish the message while the host is draining
        var stopWatch = Stopwatch.StartNew();
        var stopTask = host.StopAsync(CancellationToken.None);
        await Task.Delay(500);
        gate.TrySetResult();
        await stopTask;
        stopWatch.Stop();
        await processing;

        //assert
        stopWatch
            .Elapsed.Should()
            .BeGreaterThan(
                TimeSpan.FromMilliseconds(400),
                "shutdown must wait for the in-flight message"
            );
        stopWatch
            .Elapsed.Should()
            .BeLessThan(
                TimeSpan.FromSeconds(5),
                "shutdown must complete right after the last message finishes"
            );
    }

    [Test]
    public async Task Should_stop_when_grace_period_elapses_with_messages_still_in_flight()
    {
        //arrange: a message that never finishes and a short grace period
        var host = CreateHost(
            new HostConfiguration { ShutdownGracePeriod = TimeSpan.FromSeconds(1) }
        );
        var nextProcessor = new Mock<IMessageProcessor>();
        nextProcessor
            .Setup(x =>
                x.ProcessAsync(
                    It.IsAny<IMessageStateHandler<TestCommand>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() => new TaskCompletionSource().Task);
        _ = host.InFlightTracker.ProcessAsync(
            Mock.Of<IMessageStateHandler<TestCommand>>(),
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );

        //act
        var stopWatch = Stopwatch.StartNew();
        await host.StopAsync(CancellationToken.None);
        stopWatch.Stop();

        //assert
        stopWatch
            .Elapsed.Should()
            .BeGreaterThan(
                TimeSpan.FromMilliseconds(900),
                "shutdown must wait out the grace period for messages that never finish"
            );
        stopWatch
            .Elapsed.Should()
            .BeLessThan(TimeSpan.FromSeconds(5), "shutdown must give up after the grace period");
    }

    [Test]
    public async Task Should_release_singleton_locks_after_the_pipeline_drains()
    {
        //arrange: a singleton receiver holding a lock whose release takes a network round-trip
        var host = CreateHost(new HostConfiguration());
        var stopWatch = Stopwatch.StartNew();
        long messageDoneAt = 0;
        long releaseStartedAt = 0;

        var handle = new Mock<KnightBus.Core.Singleton.ISingletonLockHandle>();
        handle
            .Setup(x => x.RenewAsync(It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        handle
            .Setup(x => x.ReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                Interlocked.Exchange(ref releaseStartedAt, stopWatch.ElapsedMilliseconds);
                await Task.Delay(200);
            });
        var lockManager = new Mock<KnightBus.Core.Singleton.ISingletonLockManager>();
        lockManager
            .Setup(x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(handle.Object);
        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
        var singletonReceiver = new Singleton.SingletonChannelReceiver(
            underlyingReceiver.Object,
            lockManager.Object,
            Mock.Of<ILogger>(),
            teardownToken: host.TeardownToken
        );
        await singletonReceiver.StartAsync(CancellationToken.None);
        host.Receivers.Add(singletonReceiver);

        //one in-flight message that finishes after 500ms
        var nextProcessor = new Mock<IMessageProcessor>();
        nextProcessor
            .Setup(x =>
                x.ProcessAsync(
                    It.IsAny<IMessageStateHandler<TestCommand>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(async () =>
            {
                await Task.Delay(500);
                Interlocked.Exchange(ref messageDoneAt, stopWatch.ElapsedMilliseconds);
            });
        var processing = host.InFlightTracker.ProcessAsync(
            Mock.Of<IMessageStateHandler<TestCommand>>(),
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );

        //act
        await host.StopAsync(CancellationToken.None);
        await processing;

        //assert
        Interlocked
            .Read(ref releaseStartedAt)
            .Should()
            .BeGreaterThan(0, "the singleton lock must be released during shutdown");
        Interlocked
            .Read(ref releaseStartedAt)
            .Should()
            .BeGreaterThanOrEqualTo(
                Interlocked.Read(ref messageDoneAt),
                "the lock must not be released while messages are still processing"
            );
        singletonReceiver
            .TeardownCompletion.IsCompleted.Should()
            .BeTrue("shutdown must wait for the lock release to finish");
    }

    [Test]
    public async Task Should_signal_stoppable_plugins_early_and_wait_for_their_completion()
    {
        //arrange: a plugin whose stop takes longer than the message drain
        var host = CreateHost(new HostConfiguration());
        var stopWatch = Stopwatch.StartNew();
        long stopSignaledAt = 0;
        long messageDoneAt = 0;
        var stopGate = new TaskCompletionSource();
        var plugin = new Mock<IStoppablePlugin>();
        plugin
            .Setup(x => x.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Exchange(ref stopSignaledAt, stopWatch.ElapsedMilliseconds);
                return stopGate.Task;
            });
        host.Plugins.Add(plugin.Object);

        //one in-flight message that finishes after 500ms
        var nextProcessor = new Mock<IMessageProcessor>();
        nextProcessor
            .Setup(x =>
                x.ProcessAsync(
                    It.IsAny<IMessageStateHandler<TestCommand>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(async () =>
            {
                await Task.Delay(500);
                Interlocked.Exchange(ref messageDoneAt, stopWatch.ElapsedMilliseconds);
            });
        var processing = host.InFlightTracker.ProcessAsync(
            Mock.Of<IMessageStateHandler<TestCommand>>(),
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );

        //act
        var stopTask = host.StopAsync(CancellationToken.None);
        await processing;
        var winner = await Task.WhenAny(stopTask, Task.Delay(300));

        //assert
        winner
            .Should()
            .NotBe(stopTask, "shutdown must wait for stopping plugins to finish their work");
        stopGate.TrySetResult();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
        Interlocked
            .Read(ref stopSignaledAt)
            .Should()
            .BeLessThan(
                Interlocked.Read(ref messageDoneAt),
                "plugins must be told to stop while the pipeline is still draining"
            );
    }

    [Test]
    public async Task Should_complete_shutdown_when_a_plugin_fails_to_stop()
    {
        //arrange
        var host = CreateHost(new HostConfiguration());
        var plugin = new Mock<IStoppablePlugin>();
        plugin
            .Setup(x => x.StopAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("stop failed"));
        host.Plugins.Add(plugin.Object);

        //act & assert
        await host.Awaiting(x => x.StopAsync(CancellationToken.None)).Should().NotThrowAsync();
        plugin.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Should_count_in_flight_messages_and_release_on_failure()
    {
        //arrange
        var tracker = new InFlightMessageTracker();
        var gate = new TaskCompletionSource();
        var nextProcessor = new Mock<IMessageProcessor>();
        nextProcessor
            .Setup(x =>
                x.ProcessAsync(
                    It.IsAny<IMessageStateHandler<TestCommand>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() => gate.Task);

        //act & assert: counted while processing
        var processing = tracker.ProcessAsync(
            Mock.Of<IMessageStateHandler<TestCommand>>(),
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );
        tracker.Count.Should().Be(1);
        gate.TrySetResult();
        await processing;
        tracker.Count.Should().Be(0);

        //act & assert: released when processing throws
        var throwingProcessor = new Mock<IMessageProcessor>();
        throwingProcessor
            .Setup(x =>
                x.ProcessAsync(
                    It.IsAny<IMessageStateHandler<TestCommand>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException());
        await tracker
            .Awaiting(x =>
                x.ProcessAsync(
                    Mock.Of<IMessageStateHandler<TestCommand>>(),
                    Mock.Of<IPipelineInformation>(),
                    throwingProcessor.Object,
                    CancellationToken.None
                )
            )
            .Should()
            .ThrowAsync<InvalidOperationException>();
        tracker.Count.Should().Be(0);
    }

    [Test]
    public async Task Should_place_tracker_outermost_in_the_pipeline()
    {
        //arrange
        var tracker = new InFlightMessageTracker();
        var pipeline = new MiddlewarePipeline(
            Array.Empty<IMessageProcessorMiddleware>(),
            Mock.Of<IPipelineInformation>(),
            Mock.Of<ILogger>(),
            tracker
        );
        long countDuringProcessing = -1;
        var finalProcessor = new Mock<IMessageProcessor>();
        finalProcessor
            .Setup(x =>
                x.ProcessAsync(
                    It.IsAny<IMessageStateHandler<TestCommand>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                countDuringProcessing = tracker.Count;
                return Task.CompletedTask;
            });
        var stateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        stateHandler
            .Setup(x => x.MessageScope)
            .Returns(
                new Core.DependencyInjection.MicrosoftDependencyInjection(
                    new ServiceCollection().BuildServiceProvider()
                ).GetScope
            );

        //act
        var chain = pipeline.GetPipeline(finalProcessor.Object);
        await chain.ProcessAsync(stateHandler.Object, CancellationToken.None);

        //assert
        countDuringProcessing
            .Should()
            .Be(1, "the tracker must count the message through the entire pipeline");
        tracker.Count.Should().Be(0);
    }
}
