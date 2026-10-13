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
        using var cts = new CancellationTokenSource();
        await pump.StartAsync<TestCommand>((_, _) => Task.CompletedTask, cts.Token);
        await WaitFor(() => pump.Fetches > 0);

        //act
        await pump.StopFetchingAsync(CancellationToken.None);
        var fetchesWhenStopped = pump.Fetches;
        await Task.Delay(200);

        //assert
        pump.Fetches.Should().Be(fetchesWhenStopped);
        cts.Cancel();
    }

    [Test]
    public async Task Should_wait_for_a_running_handler_without_cancelling_it()
    {
        //arrange
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var handlerToken = CancellationToken.None;
        var pump = new Pump();
        using var cts = new CancellationTokenSource();
        await pump.StartAsync<TestCommand>(
            async (_, token) =>
            {
                handlerToken = token;
                started.TrySetResult();
                await release.Task;
            },
            cts.Token
        );
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        //act
        var stopping = pump.StopFetchingAsync(CancellationToken.None);
        await Task.Delay(200);

        //assert: still waiting, and the handler was left alone
        stopping.IsCompleted.Should().BeFalse();
        handlerToken.IsCancellationRequested.Should().BeFalse();

        release.SetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(10));
        cts.Cancel();
    }

    [Test]
    public async Task Should_stop_waiting_when_the_wait_is_cancelled_and_leave_the_handler_running()
    {
        //arrange
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var handlerToken = CancellationToken.None;
        var pump = new Pump();
        using var cts = new CancellationTokenSource();
        await pump.StartAsync<TestCommand>(
            async (_, token) =>
            {
                handlerToken = token;
                started.TrySetResult();
                await release.Task;
            },
            cts.Token
        );
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var waitLimit = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        //act
        var stopping = () => pump.StopFetchingAsync(waitLimit.Token);

        //assert
        await stopping.Should().ThrowAsync<OperationCanceledException>();
        handlerToken.IsCancellationRequested.Should().BeFalse();

        release.SetResult();
        cts.Cancel();
    }
}
