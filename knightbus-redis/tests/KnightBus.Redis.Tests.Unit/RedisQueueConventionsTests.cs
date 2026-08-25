using FluentAssertions;
using NUnit.Framework;

namespace KnightBus.Redis.Tests.Unit;

[TestFixture]
public class RedisQueueConventionsTests
{
    [Test]
    public void GetSagaKey_should_keep_the_existing_key_format()
    {
        RedisQueueConventions.GetSagaKey("partition", "id").Should().Be("sagas:partition:id");
    }
}
