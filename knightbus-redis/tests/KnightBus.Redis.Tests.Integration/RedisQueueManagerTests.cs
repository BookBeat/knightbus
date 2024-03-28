using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.Management;
using KnightBus.Core.PreProcessors;
using KnightBus.Redis.Management;
using KnightBus.Redis.Messages;
using KnightBus.Shared.Tests.Integration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Redis.Tests.Integration;



[TestFixture]
public class RedisQueueManagerTests : QueueManagerTests<TestCommand>
{
    private RedisBus _bus;

    public override async Task Setup()
    {
        var logger = new Mock<ILogger<RedisManagementClient>>();
        var managementClient = new RedisManagementClient(RedisTestBase.Configuration, logger.Object);
        QueueManager = new RedisQueueManager(managementClient, RedisTestBase.Configuration);
        QueueType = QueueType.Queue;
        _bus = new RedisBus(RedisTestBase.Configuration.ConnectionString, Array.Empty<IMessagePreProcessor>());
        var queues = await QueueManager.List(CancellationToken.None);
        await QueueManager.Delete("test", CancellationToken.None);
        foreach (var queue in queues)
        {
            await QueueManager.Delete(queue.Name, CancellationToken.None);
        }
    }

    public override async Task<string> CreateQueue()
    {
        var queueName = Guid.NewGuid().ToString("N");
        await RedisTestBase.Database.SetAddAsync(RedisQueueConventions.QueueListKey, queueName).ConfigureAwait(false);
        return queueName;
    }

    public override async Task<string> SendMessage(string message)
    {
        await _bus.SendAsync(new TestCommand(message));
        return AutoMessageMapper.GetQueueName<TestCommand>();
    }

    public override async Task<IMessageStateHandler<TestCommand>> GetMessageStateHandler(string queueName)
    {
        var q = new RedisQueueClient<TestCommand>(RedisTestBase.Database, queueName, RedisTestBase.Configuration.MessageSerializer, Mock.Of<ILogger>());
        var m = await q.GetMessagesAsync(1);
        return new RedisMessageStateHandler<TestCommand>(RedisTestBase.Multiplexer, RedisTestBase.Configuration, m.First(), 5, null, Mock.Of<ILogger>());
    }
}
