using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Management;
using KnightBus.LavinMQ.Management;
using KnightBus.Shared.Tests.Integration;
using Moq;
using NUnit.Framework;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ.Tests.Integration;

[TestFixture]
public class LavinMQQueueManagerTests : QueueManagerTests<TestCommand>
{
    private const int DeliveryLimit = 5;
    private LavinMQQueueManager _manager = null!;
    private IChannel _channel = null!;
    private readonly List<IChannel> _openChannels = new();

    public override async Task Setup()
    {
        QueueType = QueueType.Queue;
        _manager = new LavinMQQueueManager(LavinMQSetup.Configuration, LavinMQSetup.Connection);
        QueueManager = _manager;
        _channel = await LavinMQSetup.Connection.CreateChannelAsync();

        // Clean slate: remove the command queue (+ dead letter) and any queues left over from prior tests.
        var commandQueue = AutoMessageMapper.GetQueueName<TestCommand>();
        await SafeDeleteAsync(commandQueue);
        await SafeDeleteAsync(LavinMQQueueConventions.DeadLetterQueueName(commandQueue));
        foreach (var queue in await QueueManager.List(CancellationToken.None))
        {
            await SafeDeleteAsync(queue.Name);
            await SafeDeleteAsync(LavinMQQueueConventions.DeadLetterQueueName(queue.Name));
        }
    }

    public override async Task<string> CreateQueue()
    {
        var queueName = Guid.NewGuid().ToString("N");
        await _channel.QueueDeclareAsync(
            queueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );
        return queueName;
    }

    public override async Task<string> SendMessage(string message)
    {
        var queueName = AutoMessageMapper.GetQueueName<TestCommand>();
        await LavinMQTopology.DeclareCommandQueueAsync(
            _channel,
            queueName,
            DeliveryLimit,
            CancellationToken.None
        );
        var body = LavinMQSetup.Configuration.MessageSerializer.Serialize(new TestCommand(message));
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: new BasicProperties
            {
                Persistent = true,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            },
            body: body
        );
        return queueName;
    }

    public override async Task<IMessageStateHandler<TestCommand>> GetMessageStateHandler(
        string queueName
    )
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
        if (_channel is not null)
            await _channel.DisposeAsync();
        _manager?.Dispose();
    }

    private static async Task SafeDeleteAsync(string queue)
    {
        try
        {
            await using var channel = await LavinMQSetup.Connection.CreateChannelAsync();
            await channel.QueueDeleteAsync(queue, ifUnused: false, ifEmpty: false);
        }
        catch
        {
            // Queue may not exist
        }
    }
}
