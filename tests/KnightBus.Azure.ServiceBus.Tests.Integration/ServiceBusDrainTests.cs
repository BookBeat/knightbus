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
    private ServiceBusClient _client = null!;
    private IHost _host = null!;
    private IDrainableChannelReceiver? _receiver;
    private CancellationTokenSource? _receiverCts;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _client = new ServiceBusClient(ServiceBusSetup.ConnectionString);

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _client.DisposeAsync();

    [SetUp]
    public async Task SetUp()
    {
        await EmulatorEntities.PurgeQueue(_client, CommandQueue);
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
        var drain = _receiver!.StopFetchingAsync(CancellationToken.None);
        await SendCommand("second");
        await Task.Delay(TimeSpan.FromSeconds(1));
        var drainedWhileRunning = drain.IsCompleted;
        processor.Gate.SetResult();
        await drain.WaitAsync(TimeSpan.FromSeconds(15));
        await Task.Delay(TimeSpan.FromSeconds(1));

        //assert
        drainedWhileRunning.Should().BeFalse("the drain waits for the message being processed");
        processor.Processed.Should().Equal("first");
        var remaining = await Peek(CommandQueue);
        remaining.Should().ContainSingle();
        remaining[0].DeliveryCount.Should().Be(0, "nothing fetched the second message");
    }

    [Test]
    public async Task Should_not_throw_when_drained_twice()
    {
        //arrange
        await StartQueueReceiver(new GatedProcessor());

        //act
        await _receiver!.StopFetchingAsync(CancellationToken.None);
        var second = () => _receiver.StopFetchingAsync(CancellationToken.None);

        //assert
        await second.Should().NotThrowAsync();
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
        _ = _receiver!.StopFetchingAsync(CancellationToken.None);
        _receiverCts!.Cancel();
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
        var drain = _receiver!.StopFetchingAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1));
        var drainedWhileRunning = drain.IsCompleted;
        processor.Gate.SetResult();
        await drain.WaitAsync(TimeSpan.FromSeconds(15));

        //assert
        drainedWhileRunning.Should().BeFalse();
        processor.Processed.Should().Equal("event");
    }

    private async Task StartQueueReceiver(GatedProcessor processor)
    {
        _receiver = (IDrainableChannelReceiver)
            new ServiceBusQueueChannelReceiver<DrainCommand>(
                new DrainSettings(),
                new MicrosoftJsonSerializer(),
                new ServiceBusConfiguration(ServiceBusSetup.ConnectionString),
                _host.Services.GetRequiredService<IHostConfiguration>(),
                processor
            );
        await Start();
    }

    private async Task StartSubscriptionReceiver(GatedProcessor processor)
    {
        _receiver = (IDrainableChannelReceiver)
            new ServiceBusTopicChannelReceiver<DrainEvent>(
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
        _receiverCts = new CancellationTokenSource();
        await _receiver!.StartAsync(_receiverCts.Token);
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

// Holds every message until the test opens the gate. A cancelled message is abandoned the way
// ErrorHandlingMiddleware does it.
internal class GatedProcessor : IMessageProcessor
{
    public TaskCompletionSource Started { get; } = new();
    public TaskCompletionSource Gate { get; } = new();
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
                    _ => null,
                }
            );
            await messageStateHandler.CompleteAsync();
        }
        catch (OperationCanceledException e)
        {
            await messageStateHandler.AbandonByErrorAsync(e);
            Abandoned.TrySetResult();
        }
    }
}
