using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Management;
using KnightBus.Core;
using KnightBus.Core.Management;
using KnightBus.Core.PreProcessors;
using KnightBus.Newtonsoft;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration;

[TestFixture]
public class StorageQueueManagerTests : QueueManagerTests<TestCommand>
{
    readonly StorageBusConfiguration _configuration = new StorageBusConfiguration(
        StorageSetup.ConnectionString
    );

    public override async Task Setup()
    {
        QueueManager = new StorageQueueManager(
            _configuration,
            Array.Empty<IMessagePreProcessor>(),
            new BlobStorageMessageAttachmentProvider(_configuration)
        );
        QueueType = QueueType.Queue;
        var queues = await QueueManager.List(CancellationToken.None);
        foreach (var queue in queues)
        {
            await QueueManager.Delete(queue.Name, CancellationToken.None);
        }
    }

    public override async Task<string> CreateQueue()
    {
        var queueName = Guid.NewGuid().ToString("N");
        var client = new StorageQueueClient(
            _configuration,
            new NewtonsoftSerializer(),
            Array.Empty<IMessagePreProcessor>(),
            queueName
        );
        await client.CreateIfNotExistsAsync();
        return queueName;
    }

    public override async Task<string> SendMessage(string message)
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
        return AutoMessageMapper.GetQueueName<TestCommand>();
    }

    public override async Task<IMessageStateHandler<TestCommand>> GetMessageStateHandler(
        string queueName
    )
    {
        var client = new StorageQueueClient(
            _configuration,
            new NewtonsoftSerializer(),
            Array.Empty<IMessagePreProcessor>(),
            AutoMessageMapper.GetQueueName<TestCommand>()
        );
        var message = await client.GetMessagesAsync<TestCommand>(1, TimeSpan.FromSeconds(5));
        return new StorageQueueMessageStateHandler<TestCommand>(client, message.First(), 5, null);
    }
}
