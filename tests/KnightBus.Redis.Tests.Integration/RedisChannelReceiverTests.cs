using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.Redis.Management;
using KnightBus.Shared.Tests.Integration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Redis.Tests.Integration;

[TestFixture]
public class RedisChannelReceiverTests : ChannelReceiverTests
{
    private RedisBus _bus = null!;

    public override async Task Setup()
    {
        var managementClient = new RedisManagementClient(
            RedisTestBase.Configuration,
            Mock.Of<ILogger<RedisManagementClient>>()
        );
        var queueManager = new RedisQueueManager(managementClient, RedisTestBase.Configuration);
        await queueManager.Delete(
            AutoMessageMapper.GetQueueName<TestCommand>(),
            CancellationToken.None
        );
        _bus = new RedisBus(
            RedisTestBase.Configuration.ConnectionString!,
            Array.Empty<IMessagePreProcessor>()
        );
    }

    protected override Task SendMessage(string message) => _bus.SendAsync(new TestCommand(message));

    protected override IChannelReceiver CreateReceiver(IMessageProcessor processor) =>
        new RedisCommandChannelReceiver<TestCommand>(
            RedisTestBase.Multiplexer,
            Settings,
            RedisTestBase.Configuration.MessageSerializer,
            (RedisConfiguration)RedisTestBase.Configuration,
            HostConfiguration,
            processor
        );
}
