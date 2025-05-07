using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.Redis.Management;
using KnightBus.Shared.Tests.Integration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace KnightBus.Redis.Tests.Integration;

[TestFixture]
public class RedisMessageStateHandlerTests : MessageStateHandlerTests<TestCommand>
{
    private RedisBus _bus;

    public override async Task Setup()
    {
        _bus = new RedisBus(
            RedisTestBase.Configuration.ConnectionString,
            Array.Empty<IMessagePreProcessor>()
        );
        var logger = new Mock<ILogger<RedisManagementClient>>();
        var managementClient = new RedisManagementClient(
            RedisTestBase.Configuration,
            logger.Object
        );
        var qm = new RedisQueueManager(
            managementClient,
            RedisTestBase.Configuration,
            new RedisAttachmentProvider(
                ConnectionMultiplexer.Connect(RedisTestBase.Configuration.ConnectionString),
                RedisTestBase.Configuration
            )
        );
        _bus = new RedisBus(
            RedisTestBase.Configuration.ConnectionString,
            Array.Empty<IMessagePreProcessor>()
        );
        await qm.Delete(AutoMessageMapper.GetQueueName<TestCommand>(), CancellationToken.None);
    }

    protected override async Task<List<TestCommand>> GetMessages(int count)
    {
        var queueName = AutoMessageMapper.GetQueueName<TestCommand>();
        var q = new RedisQueueClient<TestCommand>(
            RedisTestBase.Database,
            queueName,
            RedisTestBase.Configuration.MessageSerializer,
            Mock.Of<ILogger>()
        );
        var m = await q.PeekMessagesAsync(count).ToListAsync();
        return m.Select(x => x.Message).ToList();
    }

    protected override async Task<List<TestCommand>> GetDeadLetterMessages(int count)
    {
        var queueName = AutoMessageMapper.GetQueueName<TestCommand>();
        var q = new RedisQueueClient<TestCommand>(
            RedisTestBase.Database,
            queueName,
            RedisTestBase.Configuration.MessageSerializer,
            Mock.Of<ILogger>()
        );
        var m = await q.PeekDeadlettersAsync(count).ToListAsync();
        return m.Select(x => x.Message.Body).ToList();
    }

    protected override async Task<string> SendMessage(string message)
    {
        await _bus.SendAsync(new TestCommand(message));
        return AutoMessageMapper.GetQueueName<TestCommand>();
    }

    protected override async Task<IMessageStateHandler<TestCommand>> GetMessageStateHandler()
    {
        var queueName = AutoMessageMapper.GetQueueName<TestCommand>();
        var q = new RedisQueueClient<TestCommand>(
            RedisTestBase.Database,
            queueName,
            RedisTestBase.Configuration.MessageSerializer,
            Mock.Of<ILogger>()
        );
        var m = await q.GetMessagesAsync(1);
        return new RedisMessageStateHandler<TestCommand>(
            RedisTestBase.Multiplexer,
            RedisTestBase.Configuration,
            m.First(),
            5,
            null,
            Mock.Of<ILogger>()
        );
    }
}
