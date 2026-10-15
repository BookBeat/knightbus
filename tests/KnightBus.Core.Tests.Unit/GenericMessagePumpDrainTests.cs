using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class GenericMessagePumpDrainTests
{
    private class Settings : IProcessingSettings
    {
        public int MaxConcurrentCalls => 1;
        public int PrefetchCount => 0;
        public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
        public int DeadLetterDeliveryLimit => 1;
    }

    private class Pump()
        : GenericMessagePump<TestCommand, ICommand>(new Settings(), Mock.Of<ILogger>())
    {
        private int _fetches;
        public int Fetches => Volatile.Read(ref _fetches);

        protected override async IAsyncEnumerable<TestCommand> GetMessagesAsync<TMessage>(
            int count,
            TimeSpan? lockDuration
        )
        {
            Interlocked.Increment(ref _fetches);
            await Task.CompletedTask;
            yield return new TestCommand();
        }

        protected override Task CreateChannel(Type messageType) => Task.CompletedTask;

        protected override bool ShouldCreateChannel(Exception e) => false;

        protected override Task CleanupResources() => Task.CompletedTask;

        protected override TimeSpan PollingDelay => TimeSpan.FromMilliseconds(10);

        protected override int MaxFetch => 10;
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Timed out waiting for the condition");
            await Task.Delay(10);
        }
    }

    [Test]
    public async Task Should_not_fetch_again_once_fetching_is_stopped()
    {
        //arrange
        var pump = new Pump();
        using var stopFetching = new CancellationTokenSource();
        using var cts = new CancellationTokenSource();
        await pump.StartAsync<TestCommand>(
            (_, _) => Task.CompletedTask,
            stopFetching.Token,
            cts.Token
        );
        await WaitFor(() => pump.Fetches > 0);

        //act
        await stopFetching.CancelAsync();
        await Task.Delay(50);
        var fetchesWhenStopped = pump.Fetches;
        await Task.Delay(200);

        //assert
        pump.Fetches.Should().Be(fetchesWhenStopped);
        await cts.CancelAsync();
    }

    [Test]
    public async Task Should_not_fetch_when_room_frees_up_after_fetching_has_stopped()
    {
        //arrange: one message is being handled, and the next round waits for room to fetch
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var pump = new Pump();
        using var stopFetching = new CancellationTokenSource();
        using var cts = new CancellationTokenSource();
        await pump.StartAsync<TestCommand>(
            async (_, _) =>
            {
                started.TrySetResult();
                await release.Task;
            },
            stopFetching.Token,
            cts.Token
        );
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(100);

        //act: fetching stops, then the message finishes and frees the room
        await stopFetching.CancelAsync();
        release.SetResult();
        await Task.Delay(200);

        //assert
        pump.Fetches.Should().Be(1);
        await cts.CancelAsync();
    }

    [Test]
    public async Task Should_leave_a_running_handler_alone_when_fetching_stops()
    {
        //arrange
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var handlerToken = CancellationToken.None;
        var pump = new Pump();
        using var stopFetching = new CancellationTokenSource();
        using var cts = new CancellationTokenSource();
        await pump.StartAsync<TestCommand>(
            async (_, token) =>
            {
                handlerToken = token;
                started.TrySetResult();
                await release.Task;
            },
            stopFetching.Token,
            cts.Token
        );
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        //act
        await stopFetching.CancelAsync();
        await Task.Delay(100);

        //assert
        handlerToken.IsCancellationRequested.Should().BeFalse();
        await cts.CancelAsync();
        handlerToken.IsCancellationRequested.Should().BeTrue("stopping cancels it");
        release.SetResult();
    }
}
