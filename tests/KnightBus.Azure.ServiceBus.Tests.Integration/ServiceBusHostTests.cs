using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

[TestFixture]
public class ServiceBusHostTests
{
    private static readonly string CommandQueue = AutoMessageMapper.GetQueueName<HostCommand>();
    private static readonly string FailingQueue =
        AutoMessageMapper.GetQueueName<FailingHostCommand>();
    private static readonly string Topic = AutoMessageMapper.GetQueueName<HostEvent>();
    private static readonly string Subscription = new HostEventSubscription().Name;
    private ServiceBusClient _client = null!;
    private IHost? _host;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _client = new ServiceBusClient(ServiceBusSetup.ConnectionString);

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _client.DisposeAsync();

    [SetUp]
    public async Task SetUp()
    {
        HostProcessor.Reset();
        FailingHostProcessor.Reset();
        await EmulatorEntities.PurgeQueue(_client, CommandQueue);
        await EmulatorEntities.PurgeQueue(_client, FailingQueue);
        await EmulatorEntities.PurgeSubscription(_client, Topic, Subscription);
    }

    // A host left running would keep consuming from the entities the next test uses
    [TearDown]
    public async Task TearDown() => await StopHost();

    [Test]
    public async Task Should_process_a_command_and_complete_it()
    {
        //arrange
        await StartHost<HostProcessor>();

        //act
        await Send(bus => bus.SendAsync(new HostCommand { Message = "command" }));
        var processed = await HostProcessor.Commands.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await StopHost();

        //assert
        processed.Message.Should().Be("command");
        (await PeekQueue(CommandQueue)).Should().BeEmpty();
        (await PeekQueue(CommandQueue, SubQueue.DeadLetter)).Should().BeEmpty();
    }

    [Test]
    public async Task Should_process_an_event_through_its_subscription()
    {
        //arrange
        await StartHost<HostProcessor>();

        //act
        await Send(bus => bus.PublishEventAsync(new HostEvent { Message = "event" }));
        var processed = await HostProcessor.Events.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await StopHost();

        //assert
        processed.Message.Should().Be("event");
        await using var receiver = _client.CreateReceiver(Topic, Subscription);
        (await receiver.PeekMessagesAsync(10)).Should().BeEmpty();
    }

    [Test]
    public async Task Should_dead_letter_a_command_once_the_delivery_limit_is_exceeded()
    {
        //arrange
        await StartHost<FailingHostProcessor>();

        //act
        await Send(bus => bus.SendAsync(new FailingHostCommand()));
        var deadLetters = await EmulatorEntities.Eventually(
            () => PeekQueue(FailingQueue, SubQueue.DeadLetter),
            m => m.Count > 0
        );
        await StopHost();

        //assert
        FailingHostProcessor.Invocations.Should().Be(FailingSettings.Limit);
        deadLetters.Should().ContainSingle();
        deadLetters[0].DeadLetterReason.Should().Be("MaxDeliveryCountExceeded");
        deadLetters[0]
            .DeadLetterErrorDescription.Should()
            .Be($"DeliveryCount exceeded limit of {FailingSettings.Limit}");
        deadLetters[0].ApplicationProperties.Should().Contain("ErrorMessage", "handler failed");
    }

    private async Task StartHost<TProcessor>()
    {
        _host = new HostBuilder()
            .ConfigureServices(services =>
                services
                    .UseServiceBus(new ServiceBusConfiguration(ServiceBusSetup.ConnectionString))
                    .RegisterProcessor<TProcessor>()
                    .UseTransport<ServiceBusTransport>()
            )
            .UseKnightBus()
            .Build();
        await _host.StartAsync();
    }

    private async Task StopHost()
    {
        if (_host == null)
            return;
        await _host.StopAsync();
        _host.Dispose();
        _host = null;
    }

    private async Task Send(Func<IServiceBus, Task> send)
    {
        using var scope = _host!.Services.CreateScope();
        await send(scope.ServiceProvider.GetRequiredService<IServiceBus>());
    }

    private async Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekQueue(
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

public class HostSettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 1;
    public int PrefetchCount => 0;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
    public int DeadLetterDeliveryLimit => 5;
}

public class FailingSettings : IProcessingSettings
{
    public const int Limit = 2;
    public int MaxConcurrentCalls => 1;
    public int PrefetchCount => 0;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
    public int DeadLetterDeliveryLimit => Limit;
}

public class HostProcessor
    : IProcessCommand<HostCommand, HostSettings>,
        IProcessEvent<HostEvent, HostEventSubscription, HostSettings>
{
    public static TaskCompletionSource<HostCommand> Commands { get; private set; } = new();
    public static TaskCompletionSource<HostEvent> Events { get; private set; } = new();

    public static void Reset()
    {
        Commands = new TaskCompletionSource<HostCommand>();
        Events = new TaskCompletionSource<HostEvent>();
    }

    public Task ProcessAsync(HostCommand message, CancellationToken cancellationToken)
    {
        Commands.TrySetResult(message);
        return Task.CompletedTask;
    }

    public Task ProcessAsync(HostEvent message, CancellationToken cancellationToken)
    {
        Events.TrySetResult(message);
        return Task.CompletedTask;
    }
}

public class FailingHostProcessor : IProcessCommand<FailingHostCommand, FailingSettings>
{
    private static int _invocations;

    public static int Invocations => _invocations;

    public static void Reset() => _invocations = 0;

    public Task ProcessAsync(FailingHostCommand message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _invocations);
        throw new InvalidOperationException("handler failed");
    }
}
