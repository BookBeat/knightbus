using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Messages;
using KnightBus.Tests.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace KnightBus.Shared.Tests.Integration;

/// <summary>
/// What every <see cref="IChannelReceiver"/> does with its two tokens: stopping the fetch lets the
/// running message finish and takes no more, stopping the receiver cancels the running message
/// </summary>
[TestFixture]
public abstract class ChannelReceiverTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private CancellationTokenSource? _stopFetching;
    private CancellationTokenSource? _stop;

    protected static IProcessingSettings Settings { get; } =
        new TestMessageSettings { DeadLetterDeliveryLimit = 5 };
    protected static IHostConfiguration HostConfiguration { get; } =
        new ReceiverHostConfiguration();

    [SetUp]
    public abstract Task Setup();

    protected abstract Task SendMessage(string message);

    protected abstract IChannelReceiver CreateReceiver(IMessageProcessor processor);

    [TearDown]
    public async Task StopReceiver()
    {
        if (_stop != null)
            await _stop.CancelAsync();
        _stopFetching?.Dispose();
        _stop?.Dispose();
        _stopFetching = null;
        _stop = null;
    }

    [Test]
    public async Task Should_let_the_running_message_finish_and_take_no_more_when_fetching_stops()
    {
        //arrange
        var processor = new GatedProcessor();
        await SendMessage("first");
        await Start(processor);
        await processor.Started.Task.WaitAsync(Timeout);

        //act
        await _stopFetching!.CancelAsync();
        await SendMessage("second");
        await Task.Delay(TimeSpan.FromSeconds(1));

        //assert
        processor
            .Cancelled.Task.IsCompleted.Should()
            .BeFalse("stopping the fetch lets the running message finish");
        processor.Gate.SetResult();
        await processor.Completed.Task.WaitAsync(Timeout);
        await Task.Delay(TimeSpan.FromSeconds(1));
        processor.Received.Should().Be(1, "nothing is fetched once fetching has stopped");
    }

    [Test]
    public async Task Should_cancel_the_running_message_when_stopped()
    {
        //arrange
        var processor = new GatedProcessor();
        await SendMessage("first");
        await Start(processor);
        await processor.Started.Task.WaitAsync(Timeout);

        //act
        await _stop!.CancelAsync();

        //assert
        await processor.Cancelled.Task.WaitAsync(Timeout);
    }

    private async Task Start(IMessageProcessor processor)
    {
        _stopFetching = new CancellationTokenSource();
        _stop = new CancellationTokenSource();
        await CreateReceiver(processor).StartAsync(_stopFetching.Token, _stop.Token);
    }

    private sealed class ReceiverHostConfiguration : IHostConfiguration
    {
        public IDependencyInjection DependencyInjection { get; set; } = null!;
        public ILogger Log { get; set; } = NullLogger.Instance;
        public TimeSpan ShutdownGracePeriod { get; set; } = TimeSpan.FromSeconds(30);
    }

    //Holds every message until the gate opens, and gives a cancelled one back
    private sealed class GatedProcessor : IMessageProcessor
    {
        private int _received;
        public int Received => Volatile.Read(ref _received);
        public TaskCompletionSource Started { get; } = new();
        public TaskCompletionSource Gate { get; } = new();
        public TaskCompletionSource Completed { get; } = new();
        public TaskCompletionSource Cancelled { get; } = new();

        public async Task ProcessAsync<T>(
            IMessageStateHandler<T> messageStateHandler,
            CancellationToken cancellationToken
        )
            where T : class, IMessage
        {
            Interlocked.Increment(ref _received);
            Started.TrySetResult();
            try
            {
                await Gate.Task.WaitAsync(cancellationToken);
                await messageStateHandler.CompleteAsync();
                Completed.TrySetResult();
            }
            catch (OperationCanceledException e)
            {
                Cancelled.TrySetResult();
                try
                {
                    await messageStateHandler.AbandonByErrorAsync(e);
                }
                catch
                {
                    //A stopped receiver may no longer be able to give the message back
                }
            }
        }
    }
}
