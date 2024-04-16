using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration;

public class StorageQueueMessagePumpTests
{
    private StorageQueueClient _storageQueueClient;
    private StorageQueueMessagePump _pump;

    [SetUp]
    public void SetUp()
    {
        _storageQueueClient = new StorageQueueClient(
            new StorageBusConfiguration("UseDevelopmentStorage=true"),
            new NewtonsoftSerializer(),
            new[] { new AttachmentPreProcessor(new BlobStorageMessageAttachmentProvider("UseDevelopmentStorage=true")) },
            $"{GetType().Name}-{DateTime.UtcNow.Ticks}".ToLower());
        var settings = new TestMessageSettings();
        var log = new Mock<ILogger>();
        _pump = new StorageQueueMessagePump(_storageQueueClient, settings, log.Object);
    }

    [Test]
    public async Task PumpAsync_WhenQueuesDoesNotExist_ShouldCreateQueuesWithoutThrowing()
    {
        // Arrange
        // Act
        await _pump.PumpAsync<TestCommand>((_, _) => Task.CompletedTask, CancellationToken.None);

        // Assert - should not throw
        await _storageQueueClient.GetQueueCountAsync();
        await _storageQueueClient.GetDeadLetterCountAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _storageQueueClient.DeleteIfExistsAsync();
    }
}

public class TestMessageSettings : IProcessingSettings
{
    public int MaxConcurrentCalls { get; set; } = 1;
    public TimeSpan MessageLockTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public int DeadLetterDeliveryLimit { get; set; } = 1;
    public int PrefetchCount { get; set; }
}

public class TestCommandMessageMapping : IMessageMapping<TestCommand>
{
    public string QueueName => "longrunningtestcommand";
}

public class TestCommand : IStorageQueueCommand
{
    public string Message { get; set; }
}
