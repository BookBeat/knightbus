using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class GenericMessagePumpTests
{
    private class ChannelMissingException : Exception { }

    private class TestPumpSettings : IProcessingSettings
    {
        public int MaxConcurrentCalls => 1;
        public int PrefetchCount => 0;
        public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
        public int DeadLetterDeliveryLimit => 1;
    }

    private class RecordingLogger : ILogger
    {
        public readonly ConcurrentQueue<(
            LogLevel Level,
            Exception Exception,
            string Message
        )> Entries = new();

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter
        )
        {
            Entries.Enqueue((logLevel, exception, formatter(state, exception)));
        }
    }

    private class TestMessagePump : GenericMessagePump<TestCommand, ICommand>
    {
        private readonly Func<Task> _createChannel;
        private int _getMessagesInvocations;
        private int _createChannelInvocations;
        private volatile bool _channelCreated;

        public int CreateChannelInvocations => _createChannelInvocations;
        public int GetMessagesInvocations => _getMessagesInvocations;

        public TestMessagePump(ILogger log, Func<Task> createChannel = null)
            : base(new TestPumpSettings(), log)
        {
            _createChannel = createChannel;
        }

        protected override async IAsyncEnumerable<TestCommand> GetMessagesAsync<TMessage>(
            int count,
            TimeSpan? lockDuration
        )
        {
            Interlocked.Increment(ref _getMessagesInvocations);
            if (!_channelCreated)
                throw new ChannelMissingException();
            await Task.CompletedTask;
            yield return new TestCommand();
        }

        protected override async Task CreateChannel(Type messageType)
        {
            Interlocked.Increment(ref _createChannelInvocations);
            if (_createChannel != null)
                await _createChannel();
            _channelCreated = true;
        }

        protected override bool ShouldCreateChannel(Exception e) => e is ChannelMissingException;

        protected override Task CleanupResources() => Task.CompletedTask;

        protected override TimeSpan PollingDelay => TimeSpan.FromMilliseconds(10);

        protected override int MaxFetch => 10;
    }

    [Test]
    public async Task Should_not_throw_from_pump_when_create_channel_fails()
    {
        //arrange
        var log = new RecordingLogger();
        var pump = new TestMessagePump(
            log,
            () => throw new InvalidOperationException("create failed")
        );

        //act
        var result = await pump.PumpAsync<TestCommand>(
            (_, _) => Task.CompletedTask,
            CancellationToken.None
        );

        //assert
        result.Should().BeFalse();
        log.Entries.Should()
            .Contain(
                e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException,
                "a failed channel creation must be logged, not swallowed or rethrown"
            );
    }

    [Test]
    public async Task Should_keep_polling_when_create_channel_fails_transiently()
    {
        //arrange: first CreateChannel attempt fails, second succeeds
        var failures = 0;
        var pump = new TestMessagePump(
            new RecordingLogger(),
            () =>
                Interlocked.Increment(ref failures) == 1
                    ? throw new InvalidOperationException("create failed")
                    : Task.CompletedTask
        );
        var processed = new TaskCompletionSource();
        using var cts = new CancellationTokenSource();

        //act
        await pump.StartAsync<TestCommand>(
            (_, _) =>
            {
                processed.TrySetResult();
                return Task.CompletedTask;
            },
            cts.Token
        );

        //assert: the pump must survive the failed creation, retry and process a message
        await processed
            .Task.WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        cts.Cancel();
        processed
            .Task.IsCompletedSuccessfully.Should()
            .BeTrue("the pump loop must not die when CreateChannel throws");
        pump.CreateChannelInvocations.Should().Be(2);
    }

    private class ExpiringLockPumpSettings : IProcessingSettings
    {
        public int MaxConcurrentCalls { get; init; } = 4;
        public int PrefetchCount => 0;
        public TimeSpan MessageLockTimeout { get; init; }
        public int DeadLetterDeliveryLimit => 1;
    }

    private class ExpiringLockMessagePump : GenericMessagePump<TestCommand, ICommand>
    {
        private readonly TimeSpan _fetchDelay;
        private readonly int _messagesPerFetch;

        public ExpiringLockMessagePump(
            IProcessingSettings settings,
            TimeSpan fetchDelay,
            int messagesPerFetch
        )
            : base(settings, new RecordingLogger())
        {
            _fetchDelay = fetchDelay;
            _messagesPerFetch = messagesPerFetch;
        }

        protected override async IAsyncEnumerable<TestCommand> GetMessagesAsync<TMessage>(
            int count,
            TimeSpan? lockDuration
        )
        {
            //Burn almost the entire message lock before yielding, so every message is
            //dispatched with only a sliver of its lock duration remaining
            Thread.Sleep(_fetchDelay);
            for (var i = 0; i < _messagesPerFetch; i++)
            {
                yield return new TestCommand();
            }
            await Task.CompletedTask;
        }

        protected override Task CreateChannel(Type messageType) => Task.CompletedTask;

        protected override bool ShouldCreateChannel(Exception e) => false;

        protected override Task CleanupResources() => Task.CompletedTask;

        protected override TimeSpan PollingDelay => TimeSpan.FromMilliseconds(1);

        protected override int MaxFetch => 10;
    }

    [Test]
    public async Task Should_not_leak_concurrency_slots_when_lock_expires_before_processing_starts()
    {
        const int maxConcurrent = 4;
        const int rounds = 100;

        for (var round = 0; round < rounds; round++)
        {
            var pump = new ExpiringLockMessagePump(
                new ExpiringLockPumpSettings
                {
                    MaxConcurrentCalls = maxConcurrent,
                    MessageLockTimeout = TimeSpan.FromMilliseconds(12),
                },
                fetchDelay: TimeSpan.FromMilliseconds(10),
                messagesPerFetch: maxConcurrent
            );

            //Keep the thread pool queue busy so dispatched messages don't start instantly,
            //letting the lock timeout win the race against the processing task starting
            using var noiseCts = new CancellationTokenSource();
            var noise = Task.Run(() =>
            {
                while (!noiseCts.IsCancellationRequested)
                {
                    for (var i = 0; i < 32; i++)
                    {
                        Task.Run(() => Thread.SpinWait(50_000));
                    }
                    Thread.Sleep(1);
                }
            });

            await pump.PumpAsync<TestCommand>((_, _) => Task.CompletedTask, CancellationToken.None);
            noiseCts.Cancel();
            await noise;

            //All slots must eventually come back, no matter how the lock-timeout race played out
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (pump.AvailableThreads < maxConcurrent && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            pump.AvailableThreads.Should()
                .Be(
                    maxConcurrent,
                    "a message whose lock expired before processing started must still release "
                        + $"its concurrency slot (round {round})"
                );
        }
    }
}
