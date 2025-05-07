using System;
using System.Collections.Generic;
using System.Linq;
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
public class StorageQueueMessageStateHandlerTests : MessageStateHandlerTests<TestCommand>
{
    private readonly StorageBusConfiguration _configuration = new(StorageSetup.ConnectionString);

    public override async Task Setup()
    {
        var queueManager = new StorageQueueManager(
            _configuration,
            Array.Empty<IMessagePreProcessor>(),
            new BlobStorageMessageAttachmentProvider(_configuration)
        );

        var queues = await queueManager.List(CancellationToken.None);
        foreach (var queue in queues)
        {
            await queueManager.Delete(queue.Name, CancellationToken.None);
        }
    }

    protected override async Task<List<TestCommand>> GetMessages(int count)
    {
        var qc = new StorageQueueClient(
            _configuration,
            _configuration.MessageSerializer,
            Array.Empty<IMessagePreProcessor>(),
            AutoMessageMapper.GetQueueName<TestCommand>()
        );
        var messages = await qc.PeekMessagesAsync<TestCommand>(count);
        return messages.Select(m => (TestCommand)m.Message).ToList();
    }

    protected override async Task<List<TestCommand>> GetDeadLetterMessages(int count)
    {
        var qc = new StorageQueueClient(
            _configuration,
            _configuration.MessageSerializer,
            Array.Empty<IMessagePreProcessor>(),
            AutoMessageMapper.GetQueueName<TestCommand>()
        );
        var messages = await qc.PeekDeadLettersAsync<TestCommand>(count);
        return messages.Select(m => (TestCommand)m.Message).ToList();
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

    protected override async Task<IMessageStateHandler<TestCommand>> GetMessageStateHandler()
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
