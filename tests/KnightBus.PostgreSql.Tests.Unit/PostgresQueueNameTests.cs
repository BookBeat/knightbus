using FluentAssertions;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Unit;

[TestFixture]
public class PostgresQueueNameTests
{
    [TestCase("my_queue")]
    [TestCase("MyQueue")]
    [TestCase("queue1")]
    [TestCase("_")]
    public void Should_accept_letters_digits_and_underscore(string input)
    {
        PostgresQueueName.Create(input).Value.Should().Be(input);
    }

    [TestCase("")]
    [TestCase("my-queue")]
    [TestCase("my queue")]
    [TestCase("my.queue")]
    [TestCase("my\"queue")]
    [TestCase("my'queue")]
    [TestCase("queue;DROP TABLE knightbus.metadata;--")]
    [TestCase("queue\";DROP TABLE knightbus.metadata;--")]
    [TestCase("queue\nDROP TABLE knightbus.metadata")]
    public void Should_reject_anything_that_could_escape_an_identifier(string input)
    {
        Assert.Throws<ArgumentException>(() => PostgresQueueName.Create(input));
    }

    [Test]
    public void TryCreate_should_return_false_for_invalid_input()
    {
        PostgresQueueName.TryCreate("my-queue", out _).Should().BeFalse();
        PostgresQueueName.TryCreate("", out _).Should().BeFalse();
        PostgresQueueName.TryCreate(null, out _).Should().BeFalse();
    }

    [Test]
    public void TryCreate_should_return_the_name_for_valid_input()
    {
        PostgresQueueName.TryCreate("my_queue", out var name).Should().BeTrue();
        name.Value.Should().Be("my_queue");
    }
}
