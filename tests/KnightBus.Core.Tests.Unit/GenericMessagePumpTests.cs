using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

    private class ExtendingLockPumpSettings : IProcessingSettings, IExtendMessageLockTimeout
    {
        public int MaxConcurrentCalls => 1;
        public int PrefetchCount => 0;
        public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(10);
        public int DeadLetterDeliveryLimit => 1;
        public TimeSpan ExtensionDuration => TimeSpan.FromMinutes(2);
        public TimeSpan ExtensionInterval => TimeSpan.FromSeconds(10);
    }

    private class RecordingLogger : ILogger
    {
        public readonly ConcurrentQueue<(
            LogLevel Level,
            Exception? Exception,
            string Message
        )> Entries = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Entries.Enqueue((logLevel, exception, formatter(state, exception)));
        }
    }

    private class TestMessagePump : GenericMessagePump<TestCommand, ICommand>
    {
        private readonly Func<Task>? _createChannel;
        private readonly Func<Task>? _cleanupResources;
        private readonly TimeSpan? _pollingDelay;
        private int _getMessagesInvocations;
        private int _createChannelInvocations;
        private int _cleanupResourcesInvocations;
        private volatile bool _channelCreated;

        public int CreateChannelInvocations => _createChannelInvocations;
        public int GetMessagesInvocations => _getMessagesInvocations;
        public int CleanupResourcesInvocations => _cleanupResourcesInvocations;
        public TimeSpan? LastLockDuration { get; private set; }

        public TestMessagePump(
            ILogger log,
            Func<Task>? createChannel = null,
            Func<Task>? cleanupResources = null,
            TimeSpan? pollingDelay = null,
            IProcessingSettings? settings = null
        )
            : base(settings ?? new TestPumpSettings(), log)
        {
            _createChannel = createChannel;
            _cleanupResources = cleanupResources;
            _pollingDelay = pollingDelay;
        }

        protected override async IAsyncEnumerable<TestCommand> GetMessagesAsync<TMessage>(
            int count,
            TimeSpan? lockDuration
        )
        {
            Interlocked.Increment(ref _getMessagesInvocations);
            LastLockDuration = lockDuration;
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

        protected override async Task CleanupResources()
        {
            Interlocked.Increment(ref _cleanupResourcesInvocations);
            if (_cleanupResources != null)
                await _cleanupResources();
        }

        protected override TimeSpan PollingDelay => _pollingDelay ?? TimeSpan.FromMilliseconds(10);

        protected override int MaxFetch => 10;

        public void TriggerPoll() => CancelPollingDelay();
    }

    [Test]
    public async Task Should_honor_poll_signal_sent_before_the_pump_starts()
    {
        //arrange: transports can signal new messages before the pump has started
        var pump = new TestMessagePump(
            new RecordingLogger(),
            pollingDelay: TimeSpan.FromSeconds(10)
        );
        pump.TriggerPoll();
        using var cts = new CancellationTokenSource();

        //act
        await pump.StartAsync<TestCommand>((_, _) => Task.CompletedTask, cts.Token);

        //assert: the pump must skip its first polling delay instead of discarding the signal
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (pump.GetMessagesInvocations < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, CancellationToken.None);
        }
        cts.Cancel();

        pump.GetMessagesInvocations.Should()
            .BeGreaterThanOrEqualTo(
                2,
                "a poll signal sent before the pump starts must cancel the first polling delay"
            );
    }

    [Test]
    public async Task Should_exit_polling_delay_and_cleanup_promptly_on_shutdown()
    {
        //arrange: a pump with a long polling delay that will be sleeping when shutdown hits
        var pump = new TestMessagePump(
            new RecordingLogger(),
            pollingDelay: TimeSpan.FromSeconds(10)
        );
        using var cts = new CancellationTokenSource();
        await pump.StartAsync<TestCommand>((_, _) => Task.CompletedTask, cts.Token);
        //the first pump iteration creates the channel, returns false and enters the delay
        await Task.Delay(200, CancellationToken.None);

        //act
        cts.Cancel();

        //assert: the pump must not wait out the full polling delay before cleaning up
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (pump.CleanupResourcesInvocations == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, CancellationToken.None);
        }

        pump.CleanupResourcesInvocations.Should()
            .Be(1, "a pump sleeping in its polling delay must exit promptly on shutdown");
    }

    [Test]
    public async Task Should_fetch_with_the_message_lock_timeout_by_default()
    {
        //arrange
        var pump = new TestMessagePump(new RecordingLogger());

        //act
        await pump.PumpAsync<TestCommand>((_, _) => Task.CompletedTask, CancellationToken.None);

        //assert
        pump.LastLockDuration.Should().Be(new TestPumpSettings().MessageLockTimeout);
    }

    [Test]
    public async Task Should_fetch_with_the_extension_duration_when_settings_extend_the_lock()
    {
        //arrange
        var settings = new ExtendingLockPumpSettings();
        var pump = new TestMessagePump(new RecordingLogger(), settings: settings);

        //act
        await pump.PumpAsync<TestCommand>((_, _) => Task.CompletedTask, CancellationToken.None);

        //assert
        pump.LastLockDuration.Should()
            .Be(
                settings.ExtensionDuration,
                "the transport lock is the short renewable one, not the total processing budget"
            );
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

    [Test]
    public async Task Should_cleanup_resources_when_the_pump_stops()
    {
        //arrange
        var pump = new TestMessagePump(new RecordingLogger());
        using var cts = new CancellationTokenSource();
        await pump.StartAsync<TestCommand>((_, _) => Task.CompletedTask, cts.Token);

        //act
        cts.Cancel();

        //assert
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (pump.CleanupResourcesInvocations == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, CancellationToken.None);
        }

        pump.CleanupResourcesInvocations.Should()
            .Be(1, "the pump must clean up its resources when it stops");
    }

    [Test]
    public async Task Should_log_and_not_throw_when_cleanup_resources_fails()
    {
        //arrange
        var log = new RecordingLogger();
        var pump = new TestMessagePump(
            log,
            cleanupResources: () => throw new InvalidOperationException("cleanup failed")
        );
        using var cts = new CancellationTokenSource();
        await pump.StartAsync<TestCommand>((_, _) => Task.CompletedTask, cts.Token);

        //act
        cts.Cancel();

        //assert
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (
            !log.Entries.Any(e => e.Exception is InvalidOperationException)
            && DateTime.UtcNow < deadline
        )
        {
            await Task.Delay(10, CancellationToken.None);
        }

        log.Entries.Should()
            .Contain(
                e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException,
                "a failed resource cleanup must be logged, not thrown unobserved"
            );
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

        //Tracked so the test can drain the burst, orphaned work items starve the thread
        //pool queue for tests running after this one on machines with few cores
        public readonly List<Task> NoiseTasks = new();

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
            await Task.Delay(_fetchDelay, CancellationToken.None);

            //Flood the thread pool queue right before yielding so the dispatched
            //processing tasks queue up behind the burst and don't start instantly,
            //letting the lock timeout win the race against the processing task starting
            for (var i = 0; i < 512; i++)
            {
                NoiseTasks.Add(Task.Run(() => Thread.SpinWait(20_000), CancellationToken.None));
            }

            for (var i = 0; i < _messagesPerFetch; i++)
            {
                yield return new TestCommand();
            }
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

            await pump.PumpAsync<TestCommand>((_, _) => Task.CompletedTask, CancellationToken.None);

            //All slots must eventually come back, no matter how the lock-timeout race played out
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (pump.AvailableThreads < maxConcurrent && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10, CancellationToken.None);
            }

            pump.AvailableThreads.Should()
                .Be(
                    maxConcurrent,
                    "a message whose lock expired before processing started must still release "
                        + $"its concurrency slot (round {round})"
                );

            //Drain the noise burst before the next round so it cannot pile up and starve
            //the thread pool for tests that run after this one
            await Task.WhenAll(pump.NoiseTasks);
        }
    }
}
