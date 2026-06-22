using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Shared.Tests.Integration;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ.Tests.Integration;

[TestFixture]
public class LavinMQMessageStateHandlerTests : MessageStateHandlerTests<TestCommand>
{
    private const int DeliveryLimit = 5;
    private string _queueName = null!;
    private IChannel _publishChannel = null!;
    private readonly List<IChannel> _openChannels = new();

    public override async Task Setup()
    {
        _queueName = AutoMessageMapper.GetQueueName<TestCommand>();
        _publishChannel = await LavinMQSetup.Connection.CreateChannelAsync();

        // Start from a clean queue so delivery counters and dead letters do not leak between tests.
        await SafeDeleteAsync(_queueName);
        await SafeDeleteAsync(LavinMQQueueConventions.DeadLetterQueueName(_queueName));
        await LavinMQTopology.DeclareCommandQueueAsync(
            _publishChannel,
            _queueName,
            DeliveryLimit,
            CancellationToken.None
        );
    }

    protected override Task<List<TestCommand>> GetMessages(int count) =>
        DrainAsync(_queueName, count);

    protected override Task<List<TestCommand>> GetDeadLetterMessages(int count) =>
        DrainAsync(LavinMQQueueConventions.DeadLetterQueueName(_queueName), count);

    protected override async Task SendMessage(string message)
    {
        var body = LavinMQSetup.Configuration.MessageSerializer.Serialize(new TestCommand(message));
        await _publishChannel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queueName,
            mandatory: false,
            basicProperties: CreateProperties(),
            body: body
        );
    }

    protected override Task<IMessageStateHandler<TestCommand>> GetMessageStateHandler() =>
        FetchStateHandlerAsync(_queueName);

    [TearDown]
    public async Task TearDown()
    {
        foreach (var channel in _openChannels)
        {
            try
            {
                await channel.DisposeAsync();
            }
            catch
            {
                // ignore
            }
        }

        _openChannels.Clear();
        if (_publishChannel is not null)
            await _publishChannel.DisposeAsync();
    }

    private async Task<IMessageStateHandler<TestCommand>> FetchStateHandlerAsync(string queueName)
    {
        var channel = await LavinMQSetup.Connection.CreateChannelAsync();
        _openChannels.Add(channel);

        BasicGetResult? result = null;
        for (var attempt = 0; attempt < 50 && result is null; attempt++)
        {
            result = await channel.BasicGetAsync(queueName, autoAck: false);
            if (result is null)
                await Task.Delay(50);
        }

        if (result is null)
            throw new InvalidOperationException($"No message available on queue '{queueName}'.");

        return new LavinMQMessageStateHandler<TestCommand>(
            channel,
            result.DeliveryTag,
            result.Redelivered,
            result.Body,
            result.BasicProperties,
            LavinMQSetup.Configuration.MessageSerializer,
            DeliveryLimit,
            messageScope: Mock.Of<IDependencyInjection>()
        );
    }

    private async Task<List<TestCommand>> DrainAsync(string queue, int count)
    {
        var messages = new List<TestCommand>();
        await using var channel = await LavinMQSetup.Connection.CreateChannelAsync();
        for (var i = 0; i < count; i++)
        {
            var result = await channel.BasicGetAsync(queue, autoAck: true);
            if (result is null)
                break;
            messages.Add(
                LavinMQSetup.Configuration.MessageSerializer.Deserialize<TestCommand>(
                    result.Body.Span
                )
            );
        }

        return messages;
    }

    private static BasicProperties CreateProperties() =>
        new()
        {
            Persistent = true,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };

    private static async Task SafeDeleteAsync(string queue)
    {
        try
        {
            await using var channel = await LavinMQSetup.Connection.CreateChannelAsync();
            await channel.QueueDeleteAsync(queue, ifUnused: false, ifEmpty: false);
        }
        catch
        {
            // Queue may not exist yet
        }
    }
}
