using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Management;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.Newtonsoft;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration;

[TestFixture]
public class StorageQueueChannelReceiverTests : ChannelReceiverTests
{
    private readonly StorageBusConfiguration _configuration = new(StorageSetup.ConnectionString);

    public override async Task Setup()
    {
        var queueManager = new StorageQueueManager(
            _configuration,
            Array.Empty<IMessagePreProcessor>()
        );
        foreach (var queue in await queueManager.List(CancellationToken.None))
            await queueManager.Delete(queue.Name, CancellationToken.None);
    }

    protected override async Task SendMessage(string message)
    {
        var client = new StorageQueueClient(
            _configuration,
            new NewtonsoftSerializer(),
            Array.Empty<IMessagePreProcessor>(),
            AutoMessageMapper.GetQueueName<TestCommand>()
        );
        await client.CreateIfNotExistsAsync();
        await client.SendAsync(
            new TestCommand { Message = message },
            TimeSpan.Zero,
            CancellationToken.None
        );
    }

    protected override IChannelReceiver CreateReceiver(IMessageProcessor processor) =>
        new StorageQueueChannelReceiver<TestCommand>(
            Settings,
            new NewtonsoftSerializer(),
            processor,
            HostConfiguration,
            _configuration
        );
}
