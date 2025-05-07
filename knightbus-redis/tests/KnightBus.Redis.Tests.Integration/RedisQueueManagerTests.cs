using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Management;
using KnightBus.Core.PreProcessors;
using KnightBus.Redis.Management;
using KnightBus.Shared.Tests.Integration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using static KnightBus.Redis.Tests.Integration.RedisTestBase;

namespace KnightBus.Redis.Tests.Integration;

[TestFixture]
public class RedisQueueManagerTests : QueueManagerTests<TestCommand>
{
    private RedisBus _bus;

    public override async Task Setup()
    {
        var logger = new Mock<ILogger<RedisManagementClient>>();
        var managementClient = new RedisManagementClient(Configuration, logger.Object);
        QueueManager = new RedisQueueManager(
            managementClient,
            Configuration,
            new RedisAttachmentProvider(
                ConnectionMultiplexer.Connect(Configuration.ConnectionString),
                Configuration
            )
        );
        QueueType = QueueType.Queue;
        _bus = new RedisBus(Configuration.ConnectionString, Array.Empty<IMessagePreProcessor>());
        var queues = await QueueManager.List(CancellationToken.None);
        await QueueManager.Delete(
            AutoMessageMapper.GetQueueName<TestCommand>(),
            CancellationToken.None
        );
        foreach (var queue in queues)
        {
            await QueueManager.Delete(queue.Name, CancellationToken.None);
        }
    }

    public override async Task<string> CreateQueue()
    {
        var queueName = Guid.NewGuid().ToString("N");
        await Database
            .SetAddAsync(RedisQueueConventions.QueueListKey, queueName)
            .ConfigureAwait(false);
        return queueName;
    }

    public override async Task<string> SendMessage(string message)
    {
        await _bus.SendAsync(new TestCommand(message));
        return AutoMessageMapper.GetQueueName<TestCommand>();
    }

    public override async Task<IMessageStateHandler<TestCommand>> GetMessageStateHandler(
        string queueName
    )
    {
        var q = new RedisQueueClient<TestCommand>(
            Database,
            queueName,
            Configuration.MessageSerializer,
            Mock.Of<ILogger>()
        );
        var m = await q.GetMessagesAsync(1);
        return new RedisMessageStateHandler<TestCommand>(
            Multiplexer,
            Configuration,
            m.First(),
            5,
            null,
            Mock.Of<ILogger>()
        );
    }
}
