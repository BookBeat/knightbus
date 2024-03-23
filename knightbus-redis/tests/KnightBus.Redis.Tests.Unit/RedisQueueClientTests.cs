using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;

namespace KnightBus.Redis.Tests.Unit;

[TestFixture]
public class RedisQueueClientTests
{
    private Mock<IDatabase> _db;
    private RedisQueueClient<TestCommand> _target;
    private ILogger _log = Mock.Of<ILogger>();

    [SetUp]
    public void Setup()
    {
        _db = new Mock<IDatabase>();
        _target = new RedisQueueClient<TestCommand>(_db.Object, AutoMessageMapper.GetQueueName<TestCommand>(), new MicrosoftJsonSerializer(), _log);
    }

    [Test]
    public async Task GetMessagesAsync_should_return_available_messages_when_there_are_messages()
    {
        //Arrange
        const int messageCount = 5;
        _db.Setup(x => x.ListLengthAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(messageCount);

        //Act
        var messages = await _target.GetMessagesAsync(10);

        //Assert
        messages.Length.Should().Be(messageCount);
        _db.Verify(d => d.ListRightPopLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()),
            Times.AtLeastOnce);
    }

    [Test]
    public async Task GetMessagesAsync_should_not_try_to_fetch_messages_when_there_are_none()
    {
        //Arrange
        const int messageCount = 0;
        _db.Setup(d => d.ListLengthAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(messageCount);

        //Act
        var messages = await _target.GetMessagesAsync(10);

        //Assert
        messages.Length.Should().Be(messageCount);
        _db.Verify(d => d.ListLengthAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
        _db.Verify(d => d.ListRightPopLeftPushAsync(It.IsAny<RedisKey>(), It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }
}
