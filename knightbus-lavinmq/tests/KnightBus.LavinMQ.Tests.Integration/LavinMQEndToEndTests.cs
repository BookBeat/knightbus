using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using KnightBus.LavinMQ.Messages;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ.Tests.Integration;

/// <summary>
/// Drives the full KnightBus host (receivers + middleware + bus) against a live LavinMQ broker to
/// cover the paths the shared conformance suites do not: command round-trip through the consumer,
/// event fan-out to multiple subscriptions, and delayed (scheduled) delivery.
/// </summary>
[TestFixture]
public class LavinMQEndToEndTests
{
    private IHost _host = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _host = global::Microsoft
            .Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services
                    .UseLavinMQ(c => c.ConnectionString = LavinMQSetup.ConnectionString)
                    .RegisterProcessors(typeof(LavinMQEndToEndTests).Assembly)
                    .UseTransport<LavinMQTransport>();
            })
            .UseKnightBus()
            .Build();

        await _host.StartAsync();
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [SetUp]
    public void ResetState() => E2EState.Reset();

    [Test]
    public async Task Should_round_trip_a_command_through_the_consumer()
    {
        var bus = _host.Services.GetRequiredService<ILavinMQBus>();

        await bus.SendAsync(new E2ECommand { Message = "ping" });

        var received = await E2EState.CommandReceived.Task.WaitAsync(TimeSpan.FromSeconds(30));
        received.Should().Be("ping");
    }

    [Test]
    public async Task Should_fan_out_an_event_to_all_subscriptions()
    {
        var bus = _host.Services.GetRequiredService<ILavinMQBus>();

        await bus.PublishAsync(new E2EEvent { Message = "broadcast" });

        var subscriptionA = await E2EState.EventReceivedA.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var subscriptionB = await E2EState.EventReceivedB.Task.WaitAsync(TimeSpan.FromSeconds(30));
        subscriptionA.Should().Be("broadcast");
        subscriptionB.Should().Be("broadcast");
    }

    [Test]
    public async Task Should_deliver_a_scheduled_command_only_after_the_delay()
    {
        var bus = _host.Services.GetRequiredService<ILavinMQBus>();
        var delay = TimeSpan.FromSeconds(4);

        var sentAt = DateTimeOffset.UtcNow;
        await bus.ScheduleAsync(new E2EScheduledCommand { Message = "later" }, delay);

        var arrivedAt = await E2EState.ScheduledReceived.Task.WaitAsync(TimeSpan.FromSeconds(30));
        (arrivedAt - sentAt)
            .Should()
            .BeGreaterThan(
                TimeSpan.FromSeconds(2),
                "the message must be held by the delayed exchange and not delivered immediately"
            );
    }

    [Test]
    public async Task Should_dead_letter_a_command_that_always_fails()
    {
        var bus = _host.Services.GetRequiredService<ILavinMQBus>();
        var deadLetterQueue = LavinMQQueueConventions.DeadLetterQueueName(
            AutoMessageMapper.GetQueueName<DeadLetterCommand>()
        );

        await bus.SendAsync(new DeadLetterCommand { Message = "boom" });

        // The processor always throws, so after exceeding the delivery limit the broker dead-letters it.
        var deadLettered = await WaitForDeadLetterAsync(deadLetterQueue, TimeSpan.FromSeconds(30));
        deadLettered
            .Should()
            .NotBeNull("the message must be dead-lettered after exceeding the delivery limit");
        var message = LavinMQSetup.Configuration.MessageSerializer.Deserialize<DeadLetterCommand>(
            deadLettered!.Body.Span
        );
        message.Message.Should().Be("boom");
    }

    private static async Task<BasicGetResult?> WaitForDeadLetterAsync(
        string deadLetterQueue,
        TimeSpan timeout
    )
    {
        await using var channel = await LavinMQSetup.Connection.CreateChannelAsync();
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(deadLetterQueue, autoAck: true);
            if (result is not null)
                return result;
            await Task.Delay(250);
        }

        return null;
    }
}

public static class E2EState
{
    public static TaskCompletionSource<string> CommandReceived = null!;
    public static TaskCompletionSource<string> EventReceivedA = null!;
    public static TaskCompletionSource<string> EventReceivedB = null!;
    public static TaskCompletionSource<DateTimeOffset> ScheduledReceived = null!;

    static E2EState() => Reset();

    public static void Reset()
    {
        CommandReceived = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        EventReceivedA = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        EventReceivedB = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        ScheduledReceived = new TaskCompletionSource<DateTimeOffset>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
    }
}

public class E2ECommand : ILavinMQCommand
{
    public string Message { get; set; } = string.Empty;
}

public class E2ECommandMapping : IMessageMapping<E2ECommand>
{
    public string QueueName => "lavinmq-e2e-command";
}

public class E2EScheduledCommand : ILavinMQCommand
{
    public string Message { get; set; } = string.Empty;
}

public class E2EScheduledCommandMapping : IMessageMapping<E2EScheduledCommand>
{
    public string QueueName => "lavinmq-e2e-scheduled";
}

public class E2EEvent : ILavinMQEvent
{
    public string Message { get; set; } = string.Empty;
}

public class E2EEventMapping : IMessageMapping<E2EEvent>
{
    public string QueueName => "lavinmq-e2e-event";
}

public class E2ESubscriptionA : IEventSubscription<E2EEvent>
{
    public string Name => "a";
}

public class E2ESubscriptionB : IEventSubscription<E2EEvent>
{
    public string Name => "b";
}

public class E2ESettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 5;
    public int PrefetchCount => 5;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
    public int DeadLetterDeliveryLimit => 3;
}

public class E2ECommandProcessor
    : IProcessCommand<E2ECommand, E2ESettings>,
        IProcessCommand<E2EScheduledCommand, E2ESettings>
{
    public Task ProcessAsync(E2ECommand message, CancellationToken cancellationToken)
    {
        E2EState.CommandReceived.TrySetResult(message.Message);
        return Task.CompletedTask;
    }

    public Task ProcessAsync(E2EScheduledCommand message, CancellationToken cancellationToken)
    {
        E2EState.ScheduledReceived.TrySetResult(DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}

public class E2EEventProcessorA : IProcessEvent<E2EEvent, E2ESubscriptionA, E2ESettings>
{
    public Task ProcessAsync(E2EEvent message, CancellationToken cancellationToken)
    {
        E2EState.EventReceivedA.TrySetResult(message.Message);
        return Task.CompletedTask;
    }
}

public class E2EEventProcessorB : IProcessEvent<E2EEvent, E2ESubscriptionB, E2ESettings>
{
    public Task ProcessAsync(E2EEvent message, CancellationToken cancellationToken)
    {
        E2EState.EventReceivedB.TrySetResult(message.Message);
        return Task.CompletedTask;
    }
}

public class DeadLetterCommand : ILavinMQCommand
{
    public string Message { get; set; } = string.Empty;
}

public class DeadLetterCommandMapping : IMessageMapping<DeadLetterCommand>
{
    public string QueueName => "lavinmq-e2e-deadletter";
}

public class DeadLetterProcessor : IProcessCommand<DeadLetterCommand, DeadLetterSettings>
{
    public Task ProcessAsync(DeadLetterCommand message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("This command always fails");
}

public class DeadLetterSettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 1;
    public int PrefetchCount => 1;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);

    // Dead-letter quickly so the test does not wait through many retries.
    public int DeadLetterDeliveryLimit => 1;
}
