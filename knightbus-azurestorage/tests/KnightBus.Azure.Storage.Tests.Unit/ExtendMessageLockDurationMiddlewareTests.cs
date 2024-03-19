using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Messages;
using Moq;
using NUnit.Framework;
using Range = Moq.Range;

namespace KnightBus.Azure.Storage.Tests.Unit;

[TestFixture]
public class ExtendMessageLockDurationMiddlewareTests
{
    private Mock<IDependencyInjection> _dependencyInjection;

    [Test]
    public async Task Should_allow_regular_processing_settings_to_bypass_the_renewal()
    {
        //arrange
        var pipeline = new MyPipeline
        {
            ProcessingSettings = new Mock<IProcessingSettings>().Object,
            HostConfiguration = new Mock<IHostConfiguration>().Object
        };

        var middleware = new ExtendMessageLockDurationMiddleware();
        var storageQueueClient = new Mock<IStorageQueueClient>();
        var message = new StorageQueueMessage();
        var storageQueueMessageStateHandler = new StorageQueueMessageStateHandler<MyMessage>(storageQueueClient.Object, message, 0, _dependencyInjection.Object);

        var next = new Mock<IMessageProcessor>();
        next.Setup(x => x.ProcessAsync(It.IsAny<IMessageStateHandler<MyMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.Delay(2000));

        //act
        await middleware.ProcessAsync(storageQueueMessageStateHandler, pipeline, next.Object, CancellationToken.None);

        //assert
        storageQueueClient.Verify(x => x.SetVisibilityTimeout(It.IsAny<StorageQueueMessage>(), TimeSpan.FromMilliseconds(1000), It.IsAny<CancellationToken>()), Times.Never);
        next.Verify(x => x.ProcessAsync(storageQueueMessageStateHandler, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Should_renew_lock()
    {
        //arrange
        var pipeline = new MyPipeline();
        var settings = new MySettings
        {
            ExtensionDuration = TimeSpan.FromMilliseconds(1000),
            ExtensionInterval = TimeSpan.FromMilliseconds(100)
        };
        pipeline.ProcessingSettings = settings;
        pipeline.HostConfiguration = new Mock<IHostConfiguration>().Object;
        var middleware = new ExtendMessageLockDurationMiddleware();
        var storageQueueClient = new Mock<IStorageQueueClient>();
        var message = new StorageQueueMessage();
        var storageQueueMessageStateHandler = new StorageQueueMessageStateHandler<MyMessage>(storageQueueClient.Object, message, 0, _dependencyInjection.Object);

        var next = new Mock<IMessageProcessor>();
        next.Setup(x => x.ProcessAsync(It.IsAny<IMessageStateHandler<MyMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.Delay(2000));
        //act

        await middleware.ProcessAsync(storageQueueMessageStateHandler, pipeline, next.Object, CancellationToken.None);

        //assert
        storageQueueClient.Verify(x => x.SetVisibilityTimeout(It.IsAny<StorageQueueMessage>(), TimeSpan.FromMilliseconds(1000), It.IsAny<CancellationToken>()), Times.Between(15, 20, Range.Inclusive));
    }

    [Test]
    public async Task Should_not_renew_lock_after_message_timeout()
    {
        //arrange
        var pipeline = new MyPipeline();
        var settings = new MySettings
        {
            ExtensionDuration = TimeSpan.FromMilliseconds(1000),
            ExtensionInterval = TimeSpan.FromMilliseconds(100),
            MessageLockTimeout = TimeSpan.FromMilliseconds(1000)
        };
        pipeline.ProcessingSettings = settings;
        pipeline.HostConfiguration = new Mock<IHostConfiguration>().Object;
        var middleware = new ExtendMessageLockDurationMiddleware();
        var storageQueueClient = new Mock<IStorageQueueClient>();
        var message = new StorageQueueMessage();
        var storageQueueMessageStateHandler = new StorageQueueMessageStateHandler<MyMessage>(storageQueueClient.Object, message, 0, _dependencyInjection.Object);

        var next = new Mock<IMessageProcessor>();
        next.Setup(x => x.ProcessAsync(It.IsAny<IMessageStateHandler<MyMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.Delay(2000));
        //act

        await middleware.ProcessAsync(storageQueueMessageStateHandler, pipeline, next.Object, new CancellationTokenSource(settings.MessageLockTimeout).Token);

        //assert
        storageQueueClient.Verify(x => x.SetVisibilityTimeout(It.IsAny<StorageQueueMessage>(), TimeSpan.FromMilliseconds(1000), It.IsAny<CancellationToken>()), Times.Between(5, 10, Range.Inclusive));
    }

    [Test]
    public async Task Should_not_renew_after_completion()
    {
        //arrange
        var pipeline = new MyPipeline();
        var settings = new MySettings
        {
            ExtensionDuration = TimeSpan.FromMilliseconds(1000),
            ExtensionInterval = TimeSpan.FromMilliseconds(100),
            MessageLockTimeout = TimeSpan.FromMilliseconds(1000)
        };
        pipeline.ProcessingSettings = settings;
        pipeline.HostConfiguration = new Mock<IHostConfiguration>().Object;
        var middleware = new ExtendMessageLockDurationMiddleware();
        var storageQueueClient = new Mock<IStorageQueueClient>();
        var message = new StorageQueueMessage();
        var storageQueueMessageStateHandler = new StorageQueueMessageStateHandler<MyMessage>(storageQueueClient.Object, message, 0, _dependencyInjection.Object);
        var cancellationToken = new CancellationTokenSource(settings.MessageLockTimeout).Token;

        var next = new Mock<IMessageProcessor>();
        next.Setup(x => x.ProcessAsync(It.IsAny<IMessageStateHandler<MyMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        //act

        await middleware.ProcessAsync(storageQueueMessageStateHandler, pipeline, next.Object, cancellationToken);
        // for good measure, make sure the test not passes only because we immediately return ProcessAsync
        await Task.Delay(1000);

        //assert
        storageQueueClient.Verify(x => x.SetVisibilityTimeout(It.IsAny<StorageQueueMessage>(), TimeSpan.FromMilliseconds(1000), It.IsAny<CancellationToken>()), Times.Never);
        next.Verify(x => x.ProcessAsync(storageQueueMessageStateHandler, cancellationToken), Times.Once());
    }

    [OneTimeSetUp]
    public void Setup()
    {
        _dependencyInjection = new Mock<IDependencyInjection>();
        _dependencyInjection.Setup(x => x.GetScope()).Returns(_dependencyInjection.Object);
    }
}

public class MyMessage : IStorageQueueCommand
{

}

public class MyPipeline : IPipelineInformation
{
    public Type ProcessorInterfaceType { get; }
    public IEventSubscription Subscription { get; }
    public IProcessingSettings ProcessingSettings { get; set; }
    public IHostConfiguration HostConfiguration { get; set; }
}

public class MySettings : IProcessingSettings, IExtendMessageLockTimeout
{
    public int MaxConcurrentCalls { get; }
    public int PrefetchCount { get; }
    public TimeSpan MessageLockTimeout { get; set; }
    public int DeadLetterDeliveryLimit { get; }
    public TimeSpan ExtensionDuration { get; set; }
    public TimeSpan ExtensionInterval { get; set; }
}
