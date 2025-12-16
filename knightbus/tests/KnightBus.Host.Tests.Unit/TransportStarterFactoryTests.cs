using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.Singleton;
using KnightBus.Host.MessageProcessing.Factories;
using KnightBus.Host.Tests.Unit.ExampleProcessors;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

[TestFixture]
public class TransportStarterFactoryTests
{
    [Test]
    public void Should_use_default_serializer()
    {
        //arrange
        var config = new Mock<ITransportConfiguration>();
        config.Setup(x => x.MessageSerializer).Returns(new MicrosoftJsonSerializer());
        var channel = new Mock<ITransportChannelFactory>();
        channel.Setup(x => x.CanCreate(typeof(TestCommand))).Returns(true);
        channel.Setup(x => x.Configuration).Returns(config.Object);
        var starter = new TransportStarterFactory(
            new[] { channel.Object },
            new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(
                    new ServiceCollection().BuildServiceProvider()
                ),
            }
        );
        var factory = new CommandProcessorFactory();
        //act
        starter.CreateChannelReceiver(
            factory,
            typeof(IProcessCommand<TestCommand, TestMessageSettings>),
            typeof(JsonProcessor)
        );
        //assert
        channel.Verify(
            x =>
                x.Create(
                    typeof(TestCommand),
                    null,
                    It.IsAny<IProcessingSettings>(),
                    It.IsAny<MicrosoftJsonSerializer>(),
                    It.IsAny<IHostConfiguration>(),
                    It.IsAny<IMessageProcessor>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task Should_lock_singleton_per_subscription()
    {
        // arrange
        var lockManager = new Mock<ISingletonLockManager>();
        lockManager.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        string capturedLockId = null;
        var lockHandle = new Mock<ISingletonLockHandle>();
        lockHandle.SetupGet(x => x.LeaseId).Returns("lease");
        lockHandle.SetupGet(x => x.LockId).Returns("lock");
        lockHandle
            .Setup(x => x.RenewAsync(It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        lockHandle
            .Setup(x => x.ReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        lockManager
            .Setup(x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, TimeSpan, CancellationToken>(
                (lockId, _, _) => capturedLockId = lockId
            )
            .ReturnsAsync(lockHandle.Object);

        var channelReceiver = new TestChannelReceiver();

        var config = new Mock<ITransportConfiguration>();
        config.Setup(x => x.MessageSerializer).Returns(new MicrosoftJsonSerializer());
        var channelFactory = new Mock<ITransportChannelFactory>();
        channelFactory.Setup(x => x.CanCreate(typeof(TestEvent))).Returns(true);
        channelFactory.Setup(x => x.Configuration).Returns(config.Object);
        channelFactory
            .Setup(x =>
                x.Create(
                    typeof(TestEvent),
                    It.IsAny<IEventSubscription>(),
                    It.IsAny<IProcessingSettings>(),
                    It.IsAny<IMessageSerializer>(),
                    It.IsAny<IHostConfiguration>(),
                    It.IsAny<IMessageProcessor>()
                )
            )
            .Returns(channelReceiver);

        var services = new ServiceCollection();
        services.AddSingleton(lockManager.Object);
        var provider = services.BuildServiceProvider();
        var starter = new TransportStarterFactory(
            new[] { channelFactory.Object },
            new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(provider),
                Log = NullLogger.Instance,
            }
        );
        var factory = new EventProcessorFactory();

        var receiver = starter.CreateChannelReceiver(
            factory,
            typeof(IProcessEvent<TestEvent, TestSubscription, TestTopicSettings>),
            typeof(SingletonEventProcessor)
        );

        using var cts = new CancellationTokenSource();
        await receiver.StartAsync(cts.Token);
        cts.Cancel();

        Assert.That(capturedLockId, Is.Not.Null);
        Assert.That(capturedLockId, Does.EndWith($":{new TestSubscription().Name}"));
    }

    [Test]
    public async Task Should_allow_singleton_and_non_singleton_subscriptions_on_same_topic()
    {
        // arrange
        var lockManager = new Mock<ISingletonLockManager>();
        lockManager.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        var lockHandle = new Mock<ISingletonLockHandle>();
        lockHandle
            .Setup(x => x.RenewAsync(It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        lockHandle
            .Setup(x => x.ReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        lockManager
            .Setup(x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(lockHandle.Object);

        var createdReceivers = new List<TestChannelReceiver>();

        var config = new Mock<ITransportConfiguration>();
        config.Setup(x => x.MessageSerializer).Returns(new MicrosoftJsonSerializer());
        var channelFactory = new Mock<ITransportChannelFactory>();
        channelFactory.Setup(x => x.CanCreate(typeof(TestEvent))).Returns(true);
        channelFactory.Setup(x => x.Configuration).Returns(config.Object);
        channelFactory
            .Setup(x =>
                x.Create(
                    typeof(TestEvent),
                    It.IsAny<IEventSubscription>(),
                    It.IsAny<IProcessingSettings>(),
                    It.IsAny<IMessageSerializer>(),
                    It.IsAny<IHostConfiguration>(),
                    It.IsAny<IMessageProcessor>()
                )
            )
            .Returns<
                Type,
                IEventSubscription,
                IProcessingSettings,
                IMessageSerializer,
                IHostConfiguration,
                IMessageProcessor
            >(
                (_, subscription, settings, _, _, _) =>
                {
                    var receiver = new TestChannelReceiver(subscription?.Name)
                    {
                        Settings = settings,
                    };
                    createdReceivers.Add(receiver);
                    return receiver;
                }
            );

        var services = new ServiceCollection();
        services.AddSingleton(lockManager.Object);
        var provider = services.BuildServiceProvider();
        var starter = new TransportStarterFactory(
            new[] { channelFactory.Object },
            new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(provider),
                Log = NullLogger.Instance,
            }
        );
        var factory = new EventProcessorFactory();

        var singletonReceiver = starter.CreateChannelReceiver(
            factory,
            typeof(IProcessEvent<TestEvent, TestSubscription, TestTopicSettings>),
            typeof(SingletonEventProcessor)
        );
        var regularReceiver = starter.CreateChannelReceiver(
            factory,
            typeof(IProcessEvent<TestEvent, TestSubscriptionTwo, TestTopicSettings>),
            typeof(SecondEventProcessor)
        );

        using var singletonCts = new CancellationTokenSource();
        using var regularCts = new CancellationTokenSource();

        await Task.WhenAll(
            singletonReceiver.StartAsync(singletonCts.Token),
            regularReceiver.StartAsync(regularCts.Token)
        );

        singletonCts.Cancel();
        regularCts.Cancel();

        var singletonSubName = new TestSubscription().Name;
        var regularSubName = new TestSubscriptionTwo().Name;

        Assert.That(
            createdReceivers.Single(r => r.SubscriptionName == singletonSubName).Started,
            Is.True
        );
        Assert.That(
            createdReceivers.Single(r => r.SubscriptionName == regularSubName).Started,
            Is.True
        );
    }

    private class TestChannelReceiver : IChannelReceiver
    {
        public TestChannelReceiver(string subscriptionName = null)
        {
            SubscriptionName = subscriptionName;
        }

        public string SubscriptionName { get; }
        public bool Started { get; private set; }
        public IProcessingSettings Settings { get; set; } = new TestTopicSettings();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }
    }
}

public class JsonProcessor : IProcessCommand<TestCommand, TestMessageSettings>
{
    public Task ProcessAsync(TestCommand message, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }
}
