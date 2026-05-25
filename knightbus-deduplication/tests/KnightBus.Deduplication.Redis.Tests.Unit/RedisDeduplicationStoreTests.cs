using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace KnightBus.Deduplication.Redis.Tests.Unit;

[TestFixture]
public class RedisDeduplicationStoreTests
{
    private Mock<IDatabase> _db;
    private RedisDeduplicationStore _sut;

    [SetUp]
    public void Setup()
    {
        _db = new Mock<IDatabase>();
        _sut = new RedisDeduplicationStore(_db.Object);
    }

    [Test]
    public async Task TryClaimAsync_should_call_StringSetAsync_with_NX_and_no_ttl_for_outbox_mode()
    {
        _db.Setup(d =>
                d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, When.NotExists)
            )
            .ReturnsAsync(true);

        var result = await _sut.TryClaimAsync("my-key", null, CancellationToken.None);

        result.Should().BeTrue();
        _db.Verify(d => d.StringSetAsync("my-key", "1", null, When.NotExists), Times.Once);
    }

    [Test]
    public async Task TryClaimAsync_should_call_StringSetAsync_with_NX_and_ttl_for_time_window_mode()
    {
        var ttl = TimeSpan.FromMinutes(5);
        _db.Setup(d =>
                d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists
                )
            )
            .ReturnsAsync(true);

        var result = await _sut.TryClaimAsync("my-key", ttl, CancellationToken.None);

        result.Should().BeTrue();
        _db.Verify(d => d.StringSetAsync("my-key", "1", ttl, When.NotExists), Times.Once);
    }

    [Test]
    public async Task TryClaimAsync_should_return_false_when_key_already_exists()
    {
        _db.Setup(d =>
                d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists
                )
            )
            .ReturnsAsync(false);

        var result = await _sut.TryClaimAsync("my-key", null, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Test]
    public async Task ReleaseAsync_should_call_KeyDeleteAsync()
    {
        _db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), CommandFlags.None))
            .ReturnsAsync(true);

        await _sut.ReleaseAsync("my-key", CancellationToken.None);

        _db.Verify(d => d.KeyDeleteAsync("my-key", CommandFlags.None), Times.Once);
    }
}
