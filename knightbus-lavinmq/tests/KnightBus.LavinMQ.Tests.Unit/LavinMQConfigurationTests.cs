using FluentAssertions;
using NUnit.Framework;

namespace KnightBus.LavinMQ.Tests.Unit;

[TestFixture]
public class LavinMQConfigurationTests
{
    [Test]
    public void Should_default_the_message_serializer()
    {
        var configuration = new LavinMQConfiguration();
        configuration.MessageSerializer.Should().NotBeNull();
    }

    [Test]
    public void Should_set_connection_string_from_constructor()
    {
        var configuration = new LavinMQConfiguration("amqp://guest:guest@localhost:5672");
        configuration.ConnectionString.Should().Be("amqp://guest:guest@localhost:5672");
    }

    [Test]
    public void Should_derive_dead_letter_names_from_queue_name()
    {
        LavinMQQueueConventions.DeadLetterQueueName("orders").Should().Be("orders.dl");
        LavinMQQueueConventions.DeadLetterExchangeName("orders").Should().Be("orders.dlx");
    }

    [Test]
    public void Should_compose_subscription_queue_name_from_topic_and_subscription()
    {
        LavinMQQueueConventions
            .SubscriptionQueueName("orders", "billing")
            .Should()
            .Be("orders.billing");
    }

    [Test]
    public void Should_enable_connection_and_topology_recovery_by_default()
    {
        var configuration = new LavinMQConfiguration("amqp://guest:guest@localhost:5672");

        var factory = LavinMQExtensions.BuildConnectionFactory(configuration);

        factory.AutomaticRecoveryEnabled.Should().BeTrue();
        factory.TopologyRecoveryEnabled.Should().BeTrue();
    }

    [Test]
    public void Should_apply_the_connection_factory_hook_after_the_defaults()
    {
        var invoked = false;
        var configuration = new LavinMQConfiguration("amqp://guest:guest@localhost:5672")
        {
            ConfigureConnectionFactory = factory =>
            {
                invoked = true;
                factory.AutomaticRecoveryEnabled = false;
            },
        };

        var result = LavinMQExtensions.BuildConnectionFactory(configuration);

        invoked.Should().BeTrue();
        result
            .AutomaticRecoveryEnabled.Should()
            .BeFalse("the hook runs after the defaults and can override them");
    }
}
