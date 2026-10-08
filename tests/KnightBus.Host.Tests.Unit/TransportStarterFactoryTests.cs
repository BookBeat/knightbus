using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.Singleton;
using KnightBus.Host.MessageProcessing.Factories;
using KnightBus.Host.Singleton;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public void Should_include_subscription_name_in_singleton_lock_id()
    {
        //arrange
        var config = new Mock<ITransportConfiguration>();
        config.Setup(x => x.MessageSerializer).Returns(new MicrosoftJsonSerializer());
        var channel = new Mock<ITransportChannelFactory>();
        channel.Setup(x => x.CanCreate(typeof(TestEvent))).Returns(true);
        channel.Setup(x => x.Configuration).Returns(config.Object);

        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver
            .Setup(x => x.Settings)
            .Returns(
                new SingletonProcessingSettings
                {
                    MessageLockTimeout = TimeSpan.FromMinutes(1),
                    DeadLetterDeliveryLimit = 1,
                }
            );

        channel
            .Setup(x =>
                x.Create(
                    typeof(TestEvent),
                    It.IsAny<TestSubscription>(),
                    It.IsAny<IProcessingSettings>(),
                    It.IsAny<IMessageSerializer>(),
                    It.IsAny<IHostConfiguration>(),
                    It.IsAny<IMessageProcessor>()
                )
            )
            .Returns(underlyingReceiver.Object);

        var collection = new ServiceCollection();
        collection.UseSingletonLocks(Mock.Of<ISingletonLockManager>());

        var starter = new TransportStarterFactory(
            new[] { channel.Object },
            new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(
                    collection.BuildServiceProvider()
                ),
                Log = Mock.Of<ILogger>(),
            }
        );
        var factory = new EventProcessorFactory();

        //act
        var result = starter.CreateChannelReceiver(
            factory,
            typeof(IProcessEvent<TestEvent, TestSubscription, TestTopicSettings>),
            typeof(SingletonEventProcessor)
        );

        //assert
        result.Should().BeOfType<SingletonChannelReceiver>();
        var singletonReceiver = (SingletonChannelReceiver)result;
        singletonReceiver.LockId.Should().Contain("test-sub");
    }

    [Test]
    public void Should_apply_singleton_options_to_the_receiver()
    {
        //act
        var receiver = CreateSingletonReceiver(services =>
            services.ConfigureSingletons(options =>
            {
                options.PollInterval = TimeSpan.FromSeconds(30);
                options.PollJitter = 0.5;
                options.LockDuration = TimeSpan.FromSeconds(90);
                options.RenewalInterval = TimeSpan.FromSeconds(10);
            })
        );

        //assert
        receiver.TimerInterval.Should().Be(TimeSpan.FromSeconds(30));
        receiver.PollJitter.Should().Be(0.5);
        receiver.LockDuration.Should().Be(TimeSpan.FromSeconds(90));
        receiver.LockRefreshInterval.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Test]
    public void Should_keep_the_established_lock_timing_without_options()
    {
        //act
        var receiver = CreateSingletonReceiver();

        //assert: the durations are what they were before the options existed, only jitter is new
        receiver.TimerInterval.Should().Be(TimeSpan.FromMinutes(1));
        receiver.LockDuration.Should().Be(TimeSpan.FromMinutes(1));
        receiver.LockRefreshInterval.Should().Be(TimeSpan.FromSeconds(19));
        receiver.PollJitter.Should().Be(0.2);
    }

    [Test]
    public void Should_reject_a_lock_duration_that_expires_between_renewals()
    {
        //act
        var configure = () =>
            new ServiceCollection().ConfigureSingletons(options =>
            {
                options.LockDuration = TimeSpan.FromSeconds(10);
                options.RenewalInterval = TimeSpan.FromSeconds(19);
            });

        //assert
        configure
            .Should()
            .Throw<ArgumentException>()
            .WithMessage("*LockDuration*RenewalInterval*");
    }

    private static SingletonChannelReceiver CreateSingletonReceiver(
        Action<IServiceCollection>? configure = null
    )
    {
        var config = new Mock<ITransportConfiguration>();
        config.Setup(x => x.MessageSerializer).Returns(new MicrosoftJsonSerializer());
        var channel = new Mock<ITransportChannelFactory>();
        channel.Setup(x => x.CanCreate(typeof(TestEvent))).Returns(true);
        channel.Setup(x => x.Configuration).Returns(config.Object);
        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver
            .Setup(x => x.Settings)
            .Returns(
                new SingletonProcessingSettings
                {
                    MessageLockTimeout = TimeSpan.FromMinutes(1),
                    DeadLetterDeliveryLimit = 1,
                }
            );
        channel
            .Setup(x =>
                x.Create(
                    typeof(TestEvent),
                    It.IsAny<TestSubscription>(),
                    It.IsAny<IProcessingSettings>(),
                    It.IsAny<IMessageSerializer>(),
                    It.IsAny<IHostConfiguration>(),
                    It.IsAny<IMessageProcessor>()
                )
            )
            .Returns(underlyingReceiver.Object);

        var collection = new ServiceCollection();
        collection.UseSingletonLocks(Mock.Of<ISingletonLockManager>());
        configure?.Invoke(collection);

        var starter = new TransportStarterFactory(
            new[] { channel.Object },
            new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(
                    collection.BuildServiceProvider()
                ),
                Log = Mock.Of<ILogger>(),
            }
        );
        return (SingletonChannelReceiver)
            starter.CreateChannelReceiver(
                new EventProcessorFactory(),
                typeof(IProcessEvent<TestEvent, TestSubscription, TestTopicSettings>),
                typeof(SingletonEventProcessor)
            );
    }
}

public class JsonProcessor : IProcessCommand<TestCommand, TestMessageSettings>
{
    public Task ProcessAsync(TestCommand message, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }
}

public class SingletonEventProcessor
    : IProcessEvent<TestEvent, TestSubscription, TestTopicSettings>,
        ISingletonProcessor
{
    public Task ProcessAsync(TestEvent message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
