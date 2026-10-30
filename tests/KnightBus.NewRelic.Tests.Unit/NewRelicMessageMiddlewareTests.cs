using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    public void Attribute_names_match_the_documented_nrql_contract()
    {
        NewRelicMessageMiddleware.QueueAttribute.Should().Be("knightbus.queue");
        NewRelicMessageMiddleware.TopicAttribute.Should().Be("knightbus.topic");
        NewRelicMessageMiddleware.SubscriptionAttribute.Should().Be("knightbus.subscription");
    }

    [Test]
    public async Task ProcessAsync_with_an_unmapped_type_still_calls_next()
    {
        var next = new Mock<IMessageProcessor>();
        var stateHandler = new Mock<IMessageStateHandler<UnmappedCommand>>();

        await new NewRelicMessageMiddleware().ProcessAsync(
            stateHandler.Object,
            PipelineFor(null),
            next.Object,
            CancellationToken.None
        );

        next.Verify(n => n.ProcessAsync(stateHandler.Object, CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task ProcessAsync_propagates_exceptions_from_next()
    {
        var next = new Mock<IMessageProcessor>();
        next.Setup(n =>
                n.ProcessAsync(It.IsAny<IMessageStateHandler<TestEvent>>(), CancellationToken.None)
            )
            .ThrowsAsync(new InvalidOperationException("processor failed"));

        var act = () =>
            new NewRelicMessageMiddleware().ProcessAsync(
                new Mock<IMessageStateHandler<TestEvent>>().Object,
                PipelineFor(new TestEventSubscription()),
                next.Object,
                CancellationToken.None
            );

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("processor failed");
    }
}
