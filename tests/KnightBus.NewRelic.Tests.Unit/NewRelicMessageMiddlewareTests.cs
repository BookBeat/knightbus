using System;
using System.Collections.Generic;
using AwesomeAssertions;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.NewRelicMiddleware;
using Moq;
using NUnit.Framework;

namespace KnightBus.NewRelic.Tests.Unit;

[TestFixture]
public class NewRelicMessageMiddlewareTests
{
    private static IPipelineInformation PipelineFor(IEventSubscription? subscription)
    {
        var pipeline = new Mock<IPipelineInformation>();
        pipeline.Setup(p => p.Subscription).Returns(subscription);
        return pipeline.Object;
    }

    [Test]
    public void Command_gives_the_queue_attribute()
    {
        var attributes = NewRelicMessageMiddleware.GetCustomAttributes(
            typeof(TestCommand),
            PipelineFor(null)
        );

        attributes
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new KeyValuePair<string, string>(
                        NewRelicMessageMiddleware.QueueAttribute,
                        "test-command-queue"
                    ),
                }
            );
    }

    [Test]
    public void Event_gives_the_topic_and_subscription_attributes()
    {
        var attributes = NewRelicMessageMiddleware.GetCustomAttributes(
            typeof(TestEvent),
            PipelineFor(new TestEventSubscription())
        );

        attributes
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new KeyValuePair<string, string>(
                        NewRelicMessageMiddleware.TopicAttribute,
                        "test-event-topic"
                    ),
                    new KeyValuePair<string, string>(
                        NewRelicMessageMiddleware.SubscriptionAttribute,
                        "test-subscription"
                    ),
                }
            );
    }

    [Test]
    public void Unmapped_command_gives_no_attributes_and_does_not_throw()
    {
        var act = () =>
            NewRelicMessageMiddleware.GetCustomAttributes(
                typeof(UnmappedCommand),
                PipelineFor(null)
            );

        act.Should().NotThrow().Which.Should().BeEmpty();
    }

    [Test]
    public void Unmapped_event_gives_only_the_subscription_and_does_not_throw()
    {
        var act = () =>
            NewRelicMessageMiddleware.GetCustomAttributes(
                typeof(UnmappedEvent),
                PipelineFor(new TestEventSubscription())
            );

        act.Should()
            .NotThrow()
            .Which.Should()
            .BeEquivalentTo(
                new[]
                {
                    new KeyValuePair<string, string>(
                        NewRelicMessageMiddleware.SubscriptionAttribute,
                        "test-subscription"
                    ),
                }
            );
    }

    [Test]
    public void Attribute_names_are_the_contract_the_admin_ui_queries()
    {
        NewRelicMessageMiddleware.QueueAttribute.Should().Be("knightbus.queue");
        NewRelicMessageMiddleware.TopicAttribute.Should().Be("knightbus.topic");
        NewRelicMessageMiddleware.SubscriptionAttribute.Should().Be("knightbus.subscription");
    }
}
