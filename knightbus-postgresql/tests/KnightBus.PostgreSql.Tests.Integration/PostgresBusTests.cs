using System.Text.Json;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Management;
using KnightBus.PostgreSql.Messages;
using NUnit.Framework;
using NUnit.Framework.Internal;

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
        _postgresBus = new PostgresBus(PostgresSetup.DataSource, new PostgresConfiguration());
        _postgresQueueClient = new PostgresQueueClient<TestCommand>(
            PostgresSetup.DataSource,
            new MicrosoftJsonSerializer()
        );
        _postgresManagementClient = new PostgresManagementClient(
            PostgresSetup.DataSource,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() }
        );
        await QueueInitializer.InitQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>()),
            PostgresSetup.DataSource
        );
        await QueueInitializer.InitSubscription(
            PostgresQueueName.Create(TestEventMessageMapping.TestQueueName),
            PostgresQueueName.Create(TestSubscription.TestSubscriptiomName),
            PostgresSetup.DataSource
        );
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _postgresManagementClient.DeleteQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>()),
            default
        );
        await _postgresManagementClient.DeleteSubscription(
            PostgresQueueName.Create(TestEventMessageMapping.TestQueueName).Value,
            PostgresQueueName.Create(TestSubscription.TestSubscriptiomName),
            default
        );
        await _postgresManagementClient.DeleteTopic(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestEvent>()),
            default
        );
    }

    [SetUp]
    public async Task SetUp()
    {
        await _postgresManagementClient.PurgeQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>())
        );
        await _postgresManagementClient.PurgeDeadLetterQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>())
        );
    }

    [Test]
    public async Task GetMessages_Empty()
    {
        var messages = _postgresQueueClient
            .GetMessagesAsync(1, 100, default)
            .ToBlockingEnumerable()
            .ToList();
        messages.Count.Should().Be(0);
    }

    [Test]
    public async Task InsertMessages()
    {
        await _postgresBus.SendAsync<TestCommand>(
            [
                new TestCommand { MessageBody = "hello, world!" },
                new TestCommand { MessageBody = "hello?!" },
            ],
            default
        );

        var messagesCount = (long)(
            await PostgresSetup
                .DataSource.CreateCommand(
                    $"SELECT COUNT(*) FROM knightbus.q_{AutoMessageMapper.GetQueueName<TestCommand>()};"
                )
                .ExecuteScalarAsync() ?? 0
        );

        messagesCount.Should().Be(2);
    }

    [Test]
    public async Task InsertALotOfMessages()
    {
        // Arrange
        static IEnumerable<TestCommand> GenerateCommands()
        {
            for (var i = 0; i < 100_000; i++)
            {
                yield return new TestCommand { MessageBody = $"Message {i}" };
            }
        }

        // Act
        await _postgresBus.SendAsync(GenerateCommands(), CancellationToken.None);

        // Assert
        var messagesCount = (long)(
            await PostgresSetup
                .DataSource.CreateCommand(
                    $"SELECT COUNT(*) FROM knightbus.q_{AutoMessageMapper.GetQueueName<TestCommand>()};"
                )
                .ExecuteScalarAsync() ?? 0
        );

        messagesCount.Should().Be(100_000);
    }

    [Test]
    public async Task ScheduleALotOfMessages()
    {
        // Arrange
        static IEnumerable<TestCommand> GenerateCommands()
        {
            for (var i = 0; i < 100_000; i++)
            {
                yield return new TestCommand { MessageBody = $"For future from {i}" };
            }
        }

        // Act
        await _postgresBus.ScheduleAsync(GenerateCommands(), TimeSpan.FromSeconds(3), default);

        // Assert
        var messages = _postgresQueueClient
            .GetMessagesAsync(100_000, 10, default)
            .ToBlockingEnumerable()
            .ToList();
        messages.Count.Should().Be(0);

        await Task.Delay(3000);

        var result = _postgresQueueClient
            .GetMessagesAsync(100_000, 10, default)
            .ToBlockingEnumerable()
            .ToList();

        result[0].Message.MessageBody.Should().Be("For future from 0");
        result.Count.Should().Be(100_000);
    }

    [Test]
    public async Task GetMessages()
    {
        await _postgresBus.SendAsync<TestCommand>(
            [
                new TestCommand { MessageBody = "message body 1" },
                new TestCommand { MessageBody = "message body 2" },
            ],
            default
        );

        var messages = _postgresQueueClient
            .GetMessagesAsync(2, 100, default)
            .ToBlockingEnumerable()
            .ToList();

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
                new TestCommand { MessageBody = "message body 2" },
            ],
            default
        );

        // fetch latest 2 messages
        var messages1 = _postgresQueueClient
            .GetMessagesAsync(2, 100, default)
            .ToBlockingEnumerable()
            .ToList();
        messages1.Count.Should().Be(2);

        await Task.Delay(3000);

        // fetch latest 2 messages again
        var messages2 = _postgresQueueClient
            .GetMessagesAsync(2, 100, default)
            .ToBlockingEnumerable()
            .ToList();
        messages2.Count.Should().Be(0);
    }

    [Test]
    public async Task Complete()
    {
        await _postgresBus.SendAsync<TestCommand>(
            [new TestCommand { MessageBody = "delete me" }],
            default
        );

        var message = _postgresQueueClient
            .GetMessagesAsync(1, 10, default)
            .ToBlockingEnumerable()
            .ToList();

        await _postgresQueueClient.CompleteAsync(message[0]);

        var deleted = (long)
            (
                await PostgresSetup
                    .DataSource.CreateCommand(
                        @$"
SELECT COUNT(*) FROM knightbus.q_{AutoMessageMapper.GetQueueName<TestCommand>()}
WHERE message_id = {message[0].Id}"
                    )
                    .ExecuteScalarAsync()
            )!;

        deleted.Should().Be(0);
    }

    [Test]
    public async Task AbandonByError()
    {
        await _postgresBus.SendAsync<TestCommand>(
            [new TestCommand { MessageBody = "abandon me" }],
            default
        );

        var message = _postgresQueueClient
            .GetMessagesAsync(1, 10, default)
            .ToBlockingEnumerable()
            .ToList();

        await _postgresQueueClient.AbandonByErrorAsync(
            message[0],
            new Exception("some error message")
        );

        var result = _postgresQueueClient
            .GetMessagesAsync(1, 10, default)
            .ToBlockingEnumerable()
            .ToList();
        result[0].ReadCount.Should().Be(2);
        result[0].Properties["error_message"].Should().Contain("some error message");
    }

    [Test]
    public async Task DeadLetterMessage()
    {
        await _postgresBus.SendAsync<TestCommand>(
            [new TestCommand { MessageBody = "dead letter me" }],
            default
        );

        var message = _postgresQueueClient
            .GetMessagesAsync(1, 10, default)
            .ToBlockingEnumerable()
            .ToList();
        await _postgresQueueClient.DeadLetterMessageAsync(message[0]);

        var originalMessage = (long)
            (
                await PostgresSetup
                    .DataSource.CreateCommand(
                        @$"
SELECT COUNT(*) FROM knightbus.q_{AutoMessageMapper.GetQueueName<TestCommand>()}
WHERE message_id = {message[0].Id}"
                    )
                    .ExecuteScalarAsync()
            )!;

        originalMessage.Should().Be(0);

        var deadLetters = _postgresManagementClient
            .PeekDeadLettersAsync(
                PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>()),
                10,
                default
            )
            .ToBlockingEnumerable()
            .ToList();
        deadLetters[0]
            .Message["MessageBody"]
            .ToString()
            .Should()
            .BeEquivalentTo(message[0].Message.MessageBody);
        deadLetters[0].Id.Should().Be(message[0].Id);
    }

    [Test]
    public async Task Schedule()
    {
        await _postgresBus.ScheduleAsync<TestCommand>(
            [new TestCommand { MessageBody = "for future" }],
            TimeSpan.FromSeconds(3),
            default
        );

        var messages = _postgresQueueClient
            .GetMessagesAsync(1, 10, default)
            .ToBlockingEnumerable()
            .ToList();
        messages.Count.Should().Be(0);

        await Task.Delay(3000);

        var result = _postgresQueueClient
            .GetMessagesAsync(1, 10, default)
            .ToBlockingEnumerable()
            .ToList();
        ;
        result[0].Message.MessageBody.Should().Be("for future");
    }

    [Test]
    public async Task PeekDeadLetterMessagesAsync()
    {
        await _postgresBus.SendAsync<TestCommand>(
            [new TestCommand { MessageBody = "dead letter" }],
            default
        );

        var message = _postgresQueueClient
            .GetMessagesAsync(1, 10, default)
            .ToBlockingEnumerable()
            .ToList();
        await _postgresQueueClient.DeadLetterMessageAsync(message[0]);

        var firstResult = _postgresQueueClient
            .PeekDeadLetterMessagesAsync(1, default)
            .ToBlockingEnumerable()
            .ToList();
        firstResult.Single().Id.Should().Be(message[0].Id);
        firstResult.Single().Message.Should().BeEquivalentTo(message[0].Message);

        var secondResult = _postgresQueueClient
            .PeekDeadLetterMessagesAsync(1, default)
            .ToBlockingEnumerable()
            .ToList();
        secondResult.Single().Id.Should().Be(message[0].Id);
        secondResult.Single().Message.Should().BeEquivalentTo(message[0].Message);
    }

    [Test]
    public async Task ManagementClient_SendMessages()
    {
        var message = new { MessageBody = "hello, world!" };
        var jsonBody = JsonSerializer.Serialize(message);
        await _postgresManagementClient.SendMessage(
            PostgresQueueName.Create("my_queue"),
            jsonBody,
            default
        );

        var messages = _postgresQueueClient
            .GetMessagesAsync(1, 100, default)
            .ToBlockingEnumerable()
            .ToList();

        messages[0].Message.MessageBody.Should().Be(message.MessageBody);
    }

    [Test]
    public async Task ManagementClient_PublishEvent()
    {
        var postgresTopicCLient = new PostgresSubscriptionClient<TestEvent>(
            PostgresSetup.DataSource,
            new MicrosoftJsonSerializer(),
            new TestSubscription()
        );
        var message = new { MessageBody = "hello, world!" };
        var jsonBody = JsonSerializer.Serialize(message);
        await _postgresManagementClient.PublishEvent(
            TestEventMessageMapping.TestQueueName,
            jsonBody,
            default
        );

        var messages = postgresTopicCLient
            .GetMessagesAsync(1, 100, default)
            .ToBlockingEnumerable()
            .ToList();

        messages[0].Message.MessageBody.Should().Be(message.MessageBody);
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

public class TestEvent : IPostgresEvent
{
    public string MessageBody { get; set; }
}

public class TestEventSettings : IProcessingSettings
{
    public int MaxConcurrentCalls { get; set; } = 1;
    public TimeSpan MessageLockTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public int DeadLetterDeliveryLimit { get; set; } = 1;
    public int PrefetchCount { get; set; }
}

public class TestEventMessageMapping : IMessageMapping<TestEvent>
{
    public static string TestQueueName = "my_topic";
    public string QueueName => TestQueueName;
}

public class TestSubscription : IEventSubscription<TestEvent>
{
    public static string TestSubscriptiomName = "test_mysubsciption";
    public string Name => TestSubscriptiomName;
}
