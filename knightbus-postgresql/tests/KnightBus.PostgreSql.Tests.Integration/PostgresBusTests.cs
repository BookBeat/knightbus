using FluentAssertions;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using KnightBus.PostgreSql.Management;
using KnightBus.PostgreSql.Messages;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

[TestFixture]
public class PostgresBusTests
{
    private PostgresBus _postgresBus = null!;
    private PostgresQueueClient<TestCommand> _postgresQueueClient = null!;
    private PostgresManagementClient _postgresManagementClient = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _postgresBus = new PostgresBus(PostgresTestBase.TestNpgsqlDataSource, new PostgresConfiguration());
        _postgresQueueClient = new PostgresQueueClient<TestCommand>(PostgresTestBase.TestNpgsqlDataSource, new NewtonsoftSerializer());
        _postgresManagementClient = new PostgresManagementClient(PostgresTestBase.TestNpgsqlDataSource, new NewtonsoftSerializer());
        await QueueInitializer.InitQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>()),
            PostgresTestBase.TestNpgsqlDataSource);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _postgresManagementClient.DeleteQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>()), default);
    }

    [SetUp]
    public async Task SetUp()
    {
        await _postgresManagementClient.PurgeQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>()));
        await _postgresManagementClient.PurgeDeadLetterQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>()));
    }

    [Test]
    public async Task GetMessages_Empty()
    {
        var messages = await _postgresQueueClient.GetMessagesAsync(1, 100);
        messages.Count.Should().Be(0);
    }

    [Test]
    public async Task InsertMessages()
    {
        await _postgresBus.SendAsync<TestCommand>(
        [
            new TestCommand { MessageBody = "hello, world!" },
            new TestCommand { MessageBody = "hello?!" }
        ]);

        var messagesCount = (long)
            (await PostgresTestBase.TestNpgsqlDataSource
                .CreateCommand(
                    $"SELECT COUNT(*) FROM knightbus.q_{AutoMessageMapper.GetQueueName<TestCommand>()};")
                .ExecuteScalarAsync() ?? 0);

        messagesCount.Should().Be(2);
    }

    [Test]
    public async Task GetMessages()
    {
        await _postgresBus.SendAsync<TestCommand>(
        [
            new TestCommand { MessageBody = "message body 1" },
            new TestCommand { MessageBody = "message body 2" }
        ]);

        var messages = await _postgresQueueClient.GetMessagesAsync(2, 100);

        messages.Count.Should().Be(2);
        messages[0].Message.MessageBody.Should().Be("message body 1");
        messages[0].ReadCount.Should().Be(1);
        messages[1].Message.MessageBody.Should().Be("message body 2");
        messages[1].ReadCount.Should().Be(1);
    }

    [Test]
    public async Task GetMessages_visibility_timeout()
    {
        await _postgresBus.SendAsync<TestCommand>(
        [
            new TestCommand { MessageBody = "message body 1" },
            new TestCommand { MessageBody = "message body 2" }
        ]);

        // fetch latest 2 messages
        var messages1 = await _postgresQueueClient.GetMessagesAsync(2, 100);
        messages1.Count.Should().Be(2);

        await Task.Delay(3000);

        // fetch latest 2 messages again
        var messages2 = await _postgresQueueClient.GetMessagesAsync(2, 100);
        messages2.Count.Should().Be(0);
    }

    [Test]
    public async Task Complete()
    {
        await _postgresBus.SendAsync<TestCommand>(
        [
            new TestCommand { MessageBody = "delete me" },
        ]);

        var message = await _postgresQueueClient.GetMessagesAsync(1, 10);

        await _postgresQueueClient.CompleteAsync(message[0]);

        var deleted = (long)
            (await PostgresTestBase.TestNpgsqlDataSource
                .CreateCommand(@$"
SELECT COUNT(*) FROM knightbus.q_{AutoMessageMapper.GetQueueName<TestCommand>()}
WHERE message_id = {message[0].Id}")
                .ExecuteScalarAsync())!;

        deleted.Should().Be(0);
    }

    [Test]
    public async Task AbandonByError()
    {
        await _postgresBus.SendAsync<TestCommand>(
        [
            new TestCommand { MessageBody = "abandon me" },
        ]);

        var message = await _postgresQueueClient.GetMessagesAsync(1, 10);

        await _postgresQueueClient.AbandonByErrorAsync(message[0], new Exception("some error message"));

        var result = await _postgresQueueClient.GetMessagesAsync(1, 10);
        result[0].ReadCount.Should().Be(2);
        result[0].Properties["error_message"].Should().Contain("some error message");
    }

    [Test]
    public async Task DeadLetterMessage()
    {
        await _postgresBus.SendAsync<TestCommand>(
        [
            new TestCommand { MessageBody = "dead letter me" }
        ]);

        var message = await _postgresQueueClient.GetMessagesAsync(1, 10);
        await _postgresQueueClient.DeadLetterMessageAsync(message[0]);

        var originalMessage = (long)
            (await PostgresTestBase.TestNpgsqlDataSource
                .CreateCommand(@$"
SELECT COUNT(*) FROM knightbus.q_{AutoMessageMapper.GetQueueName<TestCommand>()}
WHERE message_id = {message[0].Id}")
                .ExecuteScalarAsync())!;

        originalMessage.Should().Be(0);

        var deadLetters =
            await _postgresManagementClient.PeekDeadLettersAsync(
                PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>()), 10, default);
        deadLetters[0].Message["MessageBody"].Should().BeEquivalentTo(message[0].Message.MessageBody);
        deadLetters[0].Id.Should().Be(message[0].Id);
    }

    [Test]
    public async Task Schedule()
    {
        await _postgresBus.ScheduleAsync<TestCommand>(
        [
            new TestCommand { MessageBody = "for future" }
        ], TimeSpan.FromSeconds(3));

        var messages = await _postgresQueueClient.GetMessagesAsync(1, 10);
        messages.Count.Should().Be(0);

        await Task.Delay(3000);

        var result = await _postgresQueueClient.GetMessagesAsync(1, 10);
        result[0].Message.MessageBody.Should().Be("for future");
    }

    [Test]
    public async Task PeekDeadLetterMessagesAsync()
    {
        await _postgresBus.SendAsync<TestCommand>(
        [
            new TestCommand { MessageBody = "dead letter" }
        ]);

        var message = await _postgresQueueClient.GetMessagesAsync(1, 10);
        await _postgresQueueClient.DeadLetterMessageAsync(message[0]);

        var firstResult = await _postgresQueueClient.PeekDeadLetterMessagesAsync(1, default);
        firstResult.Single().Id.Should().Be(message[0].Id);
        firstResult.Single().Message.Should().BeEquivalentTo(message[0].Message);

        var secondResult = await _postgresQueueClient.PeekDeadLetterMessagesAsync(1, default);
        secondResult.Single().Id.Should().Be(message[0].Id);
        secondResult.Single().Message.Should().BeEquivalentTo(message[0].Message);
    }
}

public class TestCommand : IPostgresCommand
{
    public string MessageBody { get; set; }
}

public class TestMessageSettings : IProcessingSettings
{
    public int MaxConcurrentCalls { get; set; } = 1;
    public TimeSpan MessageLockTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public int DeadLetterDeliveryLimit { get; set; } = 1;
    public int PrefetchCount { get; set; }
}

public class TestCommandMessageMapping : IMessageMapping<TestCommand>
{
    public string QueueName => "my_queue";
}
