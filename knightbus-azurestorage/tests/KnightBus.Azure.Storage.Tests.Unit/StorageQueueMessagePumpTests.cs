using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Unit;

[TestFixture]
public class StorageQueueMessagePumpTests
{
    private Mock<IStorageQueueClient> _clientMock;

    [SetUp]
    public void SetUp()
    {
        _clientMock = new Mock<IStorageQueueClient>();
    }

    [Test]
    public async Task Should_prefetch_messages()
    {
        //arrange
        var settings = new TestMessageSettings
        {
            DeadLetterDeliveryLimit = 1,
            PrefetchCount = 10
        };
        var messages = new List<StorageQueueMessage>();
        for (var i = 0; i < 10; i++)
        {
            messages.Add(new StorageQueueMessage(new LongRunningTestCommand { Message = i.ToString() }));
        }

        _clientMock.Setup(x => x.GetMessagesAsync<LongRunningTestCommand>(10, It.IsAny<TimeSpan?>()))
            .ReturnsAsync(messages);
        var pump = new StorageQueueMessagePump(_clientMock.Object, settings, Mock.Of<ILogger>());
        var invocations = 0;

        Task Function(StorageQueueMessage a, CancellationToken b) => Task.FromResult(invocations++);
        //act
        await pump.PumpAsync<LongRunningTestCommand>(Function, CancellationToken.None);
        await Task.Delay(100);
        //assert
        invocations.Should().Be(10);
    }

    [Test]
    public async Task Should_not_fetch_more_messages_when_max_concurrent_is_reached()
    {
        //arrange
        var settings = new TestMessageSettings
        {
            DeadLetterDeliveryLimit = 1,
            PrefetchCount = 0,
            MaxConcurrentCalls = 1
        };
        var messages = new List<StorageQueueMessage> { new StorageQueueMessage(new LongRunningTestCommand { Message = 1.ToString() }) };


        _clientMock.Setup(x => x.GetMessagesAsync<LongRunningTestCommand>(1, It.IsAny<TimeSpan?>()))
            .ReturnsAsync(messages);
        var pump = new StorageQueueMessagePump(_clientMock.Object, settings, Mock.Of<ILogger>());
        var invocations = 0;

        async Task Function(StorageQueueMessage a, CancellationToken b)
        {
            invocations++;
            await Task.Delay(1000);
        }

        //act
        await pump.PumpAsync<LongRunningTestCommand>(Function, CancellationToken.None);
        await pump.PumpAsync<LongRunningTestCommand>(Function, CancellationToken.None);
        await Task.Delay(100);
        //assert
        invocations.Should().Be(1, "Max concurrent = 1, and Prefetch = 0");
    }

    [Test]
    public async Task Should_release_semaphore_if_exception()
    {
        //arrange
        var settings = new TestMessageSettings
        {
            DeadLetterDeliveryLimit = 1,
            PrefetchCount = 1,
            MaxConcurrentCalls = 1
        };
        var messageCount = 1;
        var messages = new List<StorageQueueMessage>();
        for (var i = 0; i < messageCount; i++)
        {
            messages.Add(new StorageQueueMessage(new LongRunningTestCommand { Message = i.ToString() }));
        }

        _clientMock.Setup(x => x.GetMessagesAsync<LongRunningTestCommand>(messageCount, It.IsAny<TimeSpan?>()))
            .ReturnsAsync(messages);
        var pump = new StorageQueueMessagePump(_clientMock.Object, settings, Mock.Of<ILogger>());
        Func<StorageQueueMessage, CancellationToken, Task> function = (a, b) =>
        {
            return Task.FromException<Exception>(new Exception());
        };
        //act
        await pump.PumpAsync<LongRunningTestCommand>(function, CancellationToken.None);
        await Task.Delay(100);
        //assert
        pump.AvailableThreads.Should().Be(1);

    }
    [Test]
    public async Task Should_prefetch_one_message_when_set_to_zero()
    {
        //arrange
        var settings = new TestMessageSettings
        {
            DeadLetterDeliveryLimit = 1,
            PrefetchCount = 0
        };
        var messages = new List<StorageQueueMessage>
        {
            new StorageQueueMessage(new LongRunningTestCommand {Message = 1.ToString()})
        };


        _clientMock.Setup(x => x.GetMessagesAsync<LongRunningTestCommand>(1, It.IsAny<TimeSpan?>())).ReturnsAsync(messages);
        var pump = new StorageQueueMessagePump(_clientMock.Object, settings, Mock.Of<ILogger>());
        var invokations = 0;
        Func<StorageQueueMessage, CancellationToken, Task> function = (a, b) => Task.FromResult(invokations++);
        //act
        await pump.PumpAsync<LongRunningTestCommand>(function, CancellationToken.None);
        await Task.Delay(100);
        //assert
        invokations.Should().Be(1);
    }

    [Test]
    public async Task Should_not_exceed_max_concurrent_when_prefetch_is_high()
    {
        //arrange
        var settings = new TestMessageSettings
        {
            DeadLetterDeliveryLimit = 1,
            PrefetchCount = 20,
            MaxConcurrentCalls = 10
        };
        var messages = new List<StorageQueueMessage>();
        for (var i = 0; i < 20; i++)
        {
            messages.Add(new StorageQueueMessage(new LongRunningTestCommand { Message = i.ToString() }));
        }

        _clientMock.Setup(x => x.GetMessagesAsync<LongRunningTestCommand>(20, It.IsAny<TimeSpan?>()))
            .ReturnsAsync(messages);
        var pump = new StorageQueueMessagePump(_clientMock.Object, settings, Mock.Of<ILogger>());
        var invocations = 0;
        Task Function(StorageQueueMessage a, CancellationToken b)
        {
            invocations++;
            return Task.Delay(1000);
        }
        //act
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        pump.PumpAsync<LongRunningTestCommand>(Function, CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        await Task.Delay(100);
        //assert
        invocations.Should().Be(10);
    }

    [Test]
    public async Task Should_not_release_semaphore_until_task_is_completed()
    {
        //arrange
        var settings = new TestMessageSettings
        {
            DeadLetterDeliveryLimit = 1,
            PrefetchCount = 1,
            MaxConcurrentCalls = 1
        };
        var messageCount = 1;
        var messages = new List<StorageQueueMessage>();
        for (var i = 0; i < messageCount; i++)
        {
            messages.Add(new StorageQueueMessage(new LongRunningTestCommand { Message = i.ToString() }));
        }

        _clientMock.Setup(x => x.GetMessagesAsync<LongRunningTestCommand>(messageCount, It.IsAny<TimeSpan?>()))
            .ReturnsAsync(messages);
        var pump = new StorageQueueMessagePump(_clientMock.Object, settings, Mock.Of<ILogger>());
        Task Function(StorageQueueMessage a, CancellationToken b)
        {
            return Task.Delay(1000);
        }
        //act
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        pump.PumpAsync<LongRunningTestCommand>(Function, CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        await Task.Delay(100);
        //assert
        pump.AvailableThreads.Should().Be(0);
    }

    [Test]
    public async Task Should_release_semaphore_when_message_lock_timeout_expires()
    {
        //arrange
        var settings = new TestMessageSettings
        {
            DeadLetterDeliveryLimit = 1,
            PrefetchCount = 1,
            MaxConcurrentCalls = 1,
            MessageLockTimeout = TimeSpan.FromMilliseconds(50)
        };
        var messageCount = 1;
        var messages = new List<StorageQueueMessage>();
        for (var i = 0; i < messageCount; i++)
        {
            messages.Add(new StorageQueueMessage(new LongRunningTestCommand { Message = i.ToString() }));
        }

        _clientMock.Setup(x => x.GetMessagesAsync<LongRunningTestCommand>(messageCount, It.IsAny<TimeSpan?>()))
            .ReturnsAsync(messages);
        var countable = new Mock<ICountable>();
        var pump = new StorageQueueMessagePump(_clientMock.Object, settings, Mock.Of<ILogger>());
        async Task Function(StorageQueueMessage a, CancellationToken b)
        {
            await Task.Delay(100, b);
            countable.Object.Count();
        }
        //act
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        pump.PumpAsync<LongRunningTestCommand>(Function, CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        await Task.Delay(150);
        //assert
        pump.AvailableThreads.Should().Be(1);
        countable.Verify(x => x.Count(), Times.Never);
    }

    [Test]
    public async Task Should_create_queue_when_it_doesnt_exists()
    {
        //arrange
        var settings = new TestMessageSettings
        {
            DeadLetterDeliveryLimit = 1,
            PrefetchCount = 10
        };

        _clientMock.Setup(x => x.GetMessagesAsync<LongRunningTestCommand>(It.IsAny<int>(), It.IsAny<TimeSpan?>()))
            .ThrowsAsync(new RequestFailedException(404, "not found", "QueueNotFound", null));
        var pump = new StorageQueueMessagePump(_clientMock.Object, settings, Mock.Of<ILogger>());

        Task Function(StorageQueueMessage a, CancellationToken b) => Task.CompletedTask;

        //act
        await pump.PumpAsync<LongRunningTestCommand>(Function, CancellationToken.None);

        //assert
        _clientMock.Verify(x => x.CreateIfNotExistsAsync(), Times.Once);
    }

    [Test]
    public async Task Should_not_create_queue_when_it_exists()
    {
        //arrange
        var settings = new TestMessageSettings
        {
            DeadLetterDeliveryLimit = 1,
            PrefetchCount = 10
        };

        _clientMock.Setup(x => x.GetMessagesAsync<LongRunningTestCommand>(It.IsAny<int>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(new List<StorageQueueMessage>());
        var pump = new StorageQueueMessagePump(_clientMock.Object, settings, Mock.Of<ILogger>());

        Task Function(StorageQueueMessage a, CancellationToken b) => Task.CompletedTask;

        //act
        await pump.PumpAsync<LongRunningTestCommand>(Function, CancellationToken.None);

        //assert
        _clientMock.Verify(x => x.CreateIfNotExistsAsync(), Times.Never);
    }

    public interface ICountable
    {
        void Count();
    }
}
