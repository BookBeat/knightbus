using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

[TestFixture]
public class ServiceBusDrainTests
{
    private static readonly string CommandQueue = AutoMessageMapper.GetQueueName<DrainCommand>();
    private static readonly string Topic = AutoMessageMapper.GetQueueName<DrainEvent>();
    private static readonly string Subscription = new DrainEventSubscription().Name;
    private static readonly string LockQueue = AutoMessageMapper.GetQueueName<DrainLockCommand>();
    private ServiceBusClient _client = null!;
    private IHost _host = null!;
    private IChannelReceiver? _receiver;
    private CancellationTokenSource? _stopFetching;
    private CancellationTokenSource? _receiverCts;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _client = new ServiceBusClient(ServiceBusSetup.ConnectionString);

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _client.DisposeAsync();

    [SetUp]
    public async Task SetUp()
    {
        await EmulatorEntities.PurgeQueue(_client, CommandQueue);
        await EmulatorEntities.PurgeQueue(_client, LockQueue);
        await EmulatorEntities.PurgeSubscription(_client, Topic, Subscription);
        //The host only supplies the dependency injection and the client factory, the receiver
        //under test is started by hand so the test controls when it drains
        _host = new HostBuilder()
            .ConfigureServices(services =>
                services
                    .UseServiceBus(new ServiceBusConfiguration(ServiceBusSetup.ConnectionString))
                    .UseTransport<ServiceBusTransport>()
            )
            .UseKnightBus()
            .Build();
        await _host.StartAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        _receiverCts?.Cancel();
        await _host.StopAsync();
        _host.Dispose();
        _stopFetching?.Dispose();
        _receiverCts?.Dispose();
    }

    [Test]
    public async Task Should_finish_the_running_message_and_take_no_more_when_drained()
    {
        //arrange
        var processor = new GatedProcessor();
        await StartQueueReceiver(processor);
        await SendCommand("first");
        await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));

        //act
        await _stopFetching!.CancelAsync();
        await SendCommand("second");
        await Task.Delay(TimeSpan.FromSeconds(1));
        var cancelledWhileDraining = processor.Cancelled.Task.IsCompleted;
        processor.Gate.SetResult();
        await EventuallyProcessed(processor, "first");
        await Task.Delay(TimeSpan.FromSeconds(1));

        //assert
        cancelledWhileDraining.Should().BeFalse("a drain lets the running message finish");
        processor.Processed.Should().Equal("first");
        var remaining = await Peek(CommandQueue);
        remaining.Should().ContainSingle();
        remaining[0].DeliveryCount.Should().Be(0, "nothing fetched the second message");
    }

    [Test]
    public async Task Should_cancel_a_message_whose_lock_is_lost_while_draining()
    {
        //arrange: the lock is renewed for a few seconds only, then it runs out
        var processor = new GatedProcessor();
        await StartLockQueueReceiver(processor);
        using (var scope = _host.Services.CreateScope())
            await scope
                .ServiceProvider.GetRequiredService<IServiceBus>()
                .SendAsync(new DrainLockCommand { Message = "slow" });
        await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));

        //act
        await _stopFetching!.CancelAsync();

        //assert: cancelled by the lost lock, the receiver itself was never stopped
        await processor.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30));
        _receiverCts!.IsCancellationRequested.Should().BeFalse();
    }

    [Test]
    public async Task Should_abandon_a_cancelled_message_without_dead_lettering_it()
    {
        //arrange
        var processor = new GatedProcessor();
        await StartQueueReceiver(processor);
        await SendCommand("cancelled");
        await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));

        //act: the drain timed out, so the receiver is cancelled while the handler runs
        await _stopFetching!.CancelAsync();
        await _receiverCts!.CancelAsync();
        await processor.Abandoned.Task.WaitAsync(TimeSpan.FromSeconds(15));

        //assert
        (await Peek(CommandQueue, SubQueue.DeadLetter))
            .Should()
            .BeEmpty();
        var queued = await EmulatorEntities.Eventually(() => Peek(CommandQueue), m => m.Count > 0);
        queued.Should().ContainSingle();
        queued[0]
            .DeliveryCount.Should()
            .Be(1, "the abandoned delivery counts, the abandon adds none");
    }

    [Test]
    public async Task Should_drain_a_subscription_the_same_way()
    {
        //arrange
        var processor = new GatedProcessor();
        await StartSubscriptionReceiver(processor);
        using (var scope = _host.Services.CreateScope())
            await scope
                .ServiceProvider.GetRequiredService<IServiceBus>()
                .PublishEventAsync(new DrainEvent { Message = "event" });
        await processor.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));

        //act
        await _stopFetching!.CancelAsync();
        await Task.Delay(TimeSpan.FromSeconds(1));
        var cancelledWhileDraining = processor.Cancelled.Task.IsCompleted;
        processor.Gate.SetResult();

        //assert
        cancelledWhileDraining.Should().BeFalse();
        await EventuallyProcessed(processor, "event");
    }

    [Test]
    public async Task Should_process_messages_again_after_being_drained_and_started_again()
    {
        //arrange: a hand-over drained and stopped the receiver, later it gets the lock back
        var processor = new GatedProcessor();
        processor.Gate.SetResult();
        await StartQueueReceiver(processor);
        await _stopFetching!.CancelAsync();
        await _receiverCts!.CancelAsync();

        //act
        await Start();
        await SendCommand("after restart");

        //assert: the earlier start's close does not touch the new client
        await EventuallyProcessed(processor, "after restart");
    }

    private static async Task EventuallyProcessed(GatedProcessor processor, string message)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!processor.Processed.Contains(message) && DateTime.UtcNow < deadline)
            await Task.Delay(100);
        processor.Processed.Should().Contain(message);
    }

    private async Task StartQueueReceiver(GatedProcessor processor)
    {
        _receiver = new ServiceBusQueueChannelReceiver<DrainCommand>(
            new DrainSettings(),
            new MicrosoftJsonSerializer(),
            new ServiceBusConfiguration(ServiceBusSetup.ConnectionString),
            _host.Services.GetRequiredService<IHostConfiguration>(),
            processor
        );
        await Start();
    }

    private async Task StartLockQueueReceiver(GatedProcessor processor)
    {
        _receiver = new ServiceBusQueueChannelReceiver<DrainLockCommand>(
            new ShortLockSettings(),
            new MicrosoftJsonSerializer(),
            new ServiceBusConfiguration(ServiceBusSetup.ConnectionString),
            _host.Services.GetRequiredService<IHostConfiguration>(),
            processor
        );
        await Start();
    }

    private async Task StartSubscriptionReceiver(GatedProcessor processor)
    {
        _receiver = new ServiceBusTopicChannelReceiver<DrainEvent>(
            new DrainSettings(),
            new MicrosoftJsonSerializer(),
            new DrainEventSubscription(),
            new ServiceBusConfiguration(ServiceBusSetup.ConnectionString),
            _host.Services.GetRequiredService<IHostConfiguration>(),
            processor
        );
        await Start();
    }

    private async Task Start()
    {
        _stopFetching = new CancellationTokenSource();
        _receiverCts = new CancellationTokenSource();
        await _receiver!.StartAsync(_stopFetching.Token, _receiverCts.Token);
    }

    private async Task SendCommand(string message)
    {
        using var scope = _host.Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<IServiceBus>()
            .SendAsync(new DrainCommand { Message = message });
    }

    private async Task<IReadOnlyList<ServiceBusReceivedMessage>> Peek(
        string queue,
        SubQueue subQueue = SubQueue.None
    )
    {
        await using var receiver = _client.CreateReceiver(
            queue,
            new ServiceBusReceiverOptions { SubQueue = subQueue }
        );
        return await receiver.PeekMessagesAsync(10);
    }
}

public class DrainSettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 1;
    public int PrefetchCount => 0;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
    public int DeadLetterDeliveryLimit => 5;
}

//The lock is renewed for this long and then runs out
public class ShortLockSettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 1;
    public int PrefetchCount => 0;
    public TimeSpan MessageLockTimeout => TimeSpan.FromSeconds(3);
    public int DeadLetterDeliveryLimit => 5;
}

// Holds every message until the test opens the gate. A cancelled message is abandoned the way
// ErrorHandlingMiddleware does it.
internal class GatedProcessor : IMessageProcessor
{
    public TaskCompletionSource Started { get; } = new();
    public TaskCompletionSource Gate { get; } = new();
    public TaskCompletionSource Cancelled { get; } = new();
    public TaskCompletionSource Abandoned { get; } = new();
    public List<string?> Processed { get; } = new();

    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        Started.TrySetResult();
        try
        {
            await Gate.Task.WaitAsync(cancellationToken);
            Processed.Add(
                messageStateHandler.GetMessage() switch
                {
                    DrainCommand c => c.Message,
                    DrainEvent e => e.Message,
                    DrainLockCommand l => l.Message,
                    _ => null,
                }
            );
            await messageStateHandler.CompleteAsync();
        }
        catch (OperationCanceledException e)
        {
            Cancelled.TrySetResult();
            await messageStateHandler.AbandonByErrorAsync(e);
            Abandoned.TrySetResult();
        }
    }
}
