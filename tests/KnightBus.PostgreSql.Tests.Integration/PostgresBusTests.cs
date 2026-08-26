using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.Messages;
using KnightBus.PostgreSql.Management;
using KnightBus.PostgreSql.Messages;
using Npgsql;
using NpgsqlTypes;
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
        _postgresBus = new PostgresBus(PostgresSetup.DataSource, new PostgresConfiguration(), []);
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
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _postgresManagementClient.DeleteQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<TestCommand>()),
            default
        );
        await PostgresSetup
            .DataSource.CreateCommand(
                @"
DROP TABLE IF EXISTS knightbus.s_bus_test_topic_bus_sub;
DROP TABLE IF EXISTS knightbus.dlq_bus_test_topic_bus_sub;
DROP TABLE IF EXISTS knightbus.t_bus_test_topic;"
            )
            .ExecuteNonQueryAsync();
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

        result.Count.Should().Be(100_000);
        // UPDATE ... RETURNING does not preserve the CTE's ORDER BY, so assert membership
        result.Select(m => m.Message.MessageBody).Should().Contain("For future from 0");
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
        messages[0].Properties.Should().BeEmpty();
        messages[1].Message.MessageBody.Should().Be("message body 2");
        messages[1].ReadCount.Should().Be(1);
        messages[1].Properties.Should().BeEmpty();
    }

    [Test]
    public async Task SendAsync_WithPreProcessors_StoresPropertiesOnMessage()
    {
        var bus = new PostgresBus(
            PostgresSetup.DataSource,
            new PostgresConfiguration(),
            [new TestPreProcessor()]
        );

        await bus.SendAsync<TestCommand>(
            [
                new TestCommand { MessageBody = "first" },
                new TestCommand { MessageBody = "skip this one" },
                new TestCommand { MessageBody = "third" },
            ],
            default
        );

        var messages = _postgresQueueClient
            .GetMessagesAsync(3, 100, default)
            .ToBlockingEnumerable()
            .ToList();

        messages.Count.Should().Be(3);
        messages[0].Properties["trace_id"].Should().Be("first");
        messages[1].Properties.Should().BeEmpty();
        messages[2].Properties["trace_id"].Should().Be("third");
    }

    [Test]
    public async Task SendAsync_ManyMessagesWithPreProcessors_StoresPropertiesOnMessage()
    {
        var bus = new PostgresBus(
            PostgresSetup.DataSource,
            new PostgresConfiguration(),
            [new TestPreProcessor()]
        );

        // 50 or more messages take the binary COPY path
        var commands = Enumerable
            .Range(0, 60)
            .Select(i => new TestCommand
            {
                MessageBody = i % 2 == 0 ? $"Message {i}" : $"skip {i}",
            })
            .ToList();
        await bus.SendAsync<TestCommand>(commands, default);

        var messages = _postgresQueueClient
            .GetMessagesAsync(60, 100, default)
            .ToBlockingEnumerable()
            .ToList();

        messages.Count.Should().Be(60);
        foreach (var message in messages)
        {
            if (message.Message.MessageBody.StartsWith("skip"))
                message.Properties.Should().BeEmpty();
            else
                message.Properties["trace_id"].Should().Be(message.Message.MessageBody);
        }
    }

    [Test]
    public async Task PublishAsync_WithPreProcessors_StoresPropertiesOnMessage()
    {
        await InitBusTestSubscription();
        var bus = new PostgresBus(
            PostgresSetup.DataSource,
            new PostgresConfiguration(),
            [new TestPreProcessor()]
        );

        var events = Enumerable
            .Range(0, 6)
            .Select(i => new BusTestEvent(i % 2 == 0 ? $"event {i}" : $"skip {i}"))
            .ToList();
        await bus.PublishAsync(events, default);

        var messages = CreateBusTestSubscriptionClient()
            .GetMessagesAsync(6, 100, default)
            .ToBlockingEnumerable()
            .ToList();

        messages.Count.Should().Be(6);
        foreach (var message in messages)
        {
            if (message.Message.Value.StartsWith("skip"))
                message.Properties.Should().BeEmpty();
            else
                message.Properties["trace_id"].Should().Be(message.Message.Value);
        }
    }

    [Test]
    public async Task PublishAsync_LegacyTwoArgumentFunction_StillWorks()
    {
        await InitBusTestSubscription();

        await using var connection = await PostgresSetup.DataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "select knightbus.publish_events($1, $2)",
            connection
        );
        cmd.Parameters.Add(
            new NpgsqlParameter { Value = "bus_test_topic", NpgsqlDbType = NpgsqlDbType.Text }
        );
        cmd.Parameters.Add(
            new NpgsqlParameter
            {
                Value = new[]
                {
                    new MicrosoftJsonSerializer().Serialize(new BusTestEvent("legacy")),
                },
                NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Jsonb,
            }
        );
        await cmd.ExecuteNonQueryAsync();

        var messages = CreateBusTestSubscriptionClient()
            .GetMessagesAsync(1, 100, default)
            .ToBlockingEnumerable()
            .ToList();

        messages.Single().Message.Value.Should().Be("legacy");
        messages.Single().Properties.Should().BeEmpty();
    }

    [Test]
    public async Task PublishAsync_MissingPropertiesOverload_CreatesItAndRetries()
    {
        await InitBusTestSubscription();
        // A database initialized before 4.0.0 only has the two-argument function
        await PostgresSetup
            .DataSource.CreateCommand(
                "DROP FUNCTION IF EXISTS knightbus.publish_events(text, jsonb[], jsonb[]);"
            )
            .ExecuteNonQueryAsync();
        var bus = new PostgresBus(
            PostgresSetup.DataSource,
            new PostgresConfiguration(),
            [new TestPreProcessor()]
        );

        await bus.PublishAsync(new BusTestEvent("healed"), default);

        var messages = CreateBusTestSubscriptionClient()
            .GetMessagesAsync(1, 100, default)
            .ToBlockingEnumerable()
            .ToList();

        messages.Single().Message.Value.Should().Be("healed");
        messages.Single().Properties["trace_id"].Should().Be("healed");
    }

    private static Task InitBusTestSubscription() =>
        QueueInitializer.InitSubscription(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<BusTestEvent>()),
            PostgresQueueName.Create(new BusTestEventSubscription().Name),
            PostgresSetup.DataSource
        );

    private static PostgresSubscriptionClient<BusTestEvent> CreateBusTestSubscriptionClient() =>
        new(
            PostgresSetup.DataSource,
            new MicrosoftJsonSerializer(),
            new BusTestEventSubscription()
        );

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
    public async Task GetMessages_sub_second_visibility_timeout_is_not_truncated()
    {
        await _postgresBus.SendAsync<TestCommand>(
            [new TestCommand { MessageBody = "lock me briefly" }],
            default
        );

        var message = _postgresQueueClient
            .GetMessagesAsync(1, TimeSpan.FromMilliseconds(500), default)
            .ToBlockingEnumerable()
            .Single();

        var lockedForUnderASecond = (bool)
            (
                await PostgresSetup
                    .DataSource.CreateCommand(
                        @$"
SELECT visibility_timeout > clock_timestamp()
   AND visibility_timeout <= clock_timestamp() + interval '500 milliseconds'
FROM knightbus.q_{AutoMessageMapper.GetQueueName<TestCommand>()}
WHERE message_id = {message.Id}"
                    )
                    .ExecuteScalarAsync()
            )!;

        lockedForUnderASecond.Should().BeTrue();
    }

    [Test]
    public async Task SetVisibilityTimeout_with_a_cancelled_token_throws_and_leaves_the_message()
    {
        await _postgresBus.SendAsync<TestCommand>(
            [new TestCommand { MessageBody = "cancelled" }],
            default
        );
        var message = _postgresQueueClient
            .GetMessagesAsync(1, 0, default)
            .ToBlockingEnumerable()
            .Single();
        var cancelled = new CancellationToken(canceled: true);

        await _postgresQueueClient
            .Awaiting(c =>
                c.SetVisibilityTimeoutAsync(message, TimeSpan.FromSeconds(30), cancelled)
            )
            .Should()
            .ThrowAsync<OperationCanceledException>();

        var messages = _postgresQueueClient
            .GetMessagesAsync(1, 0, default)
            .ToBlockingEnumerable()
            .ToList();
        messages.Should().ContainSingle();
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
    public async Task Schedule_FractionalDelayUnderCommaDecimalCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
        try
        {
            await _postgresBus.ScheduleAsync<TestCommand>(
                [new TestCommand { MessageBody = "fractional delay" }],
                TimeSpan.FromMilliseconds(1500),
                default
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        await Task.Delay(2000);

        var result = _postgresQueueClient
            .GetMessagesAsync(1, 10, default)
            .ToBlockingEnumerable()
            .ToList();
        result.Single().Message.MessageBody.Should().Be("fractional delay");
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
}

public class TestCommand : IPostgresCommand
{
    public string MessageBody { get; set; }
}

public class BusTestEvent : IPostgresEvent
{
    public string Value { get; set; }

    public BusTestEvent(string value)
    {
        Value = value;
    }
}

public class BusTestEventMapping : IMessageMapping<BusTestEvent>
{
    public string QueueName => "bus_test_topic";
}

public class BusTestEventSubscription : IEventSubscription<BusTestEvent>
{
    public string Name => "bus_sub";
}

public class TestPreProcessor : IMessagePreProcessor
{
    public Task<IDictionary<string, object>> PreProcess<T>(
        T message,
        CancellationToken cancellationToken
    )
        where T : IMessage
    {
        var body = message switch
        {
            TestCommand command => command.MessageBody,
            BusTestEvent busEvent => busEvent.Value,
            _ => "unknown",
        };

        // Mirrors AttachmentPreProcessor, which returns nothing for messages without attachments
        IDictionary<string, object> properties = body.StartsWith("skip")
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["trace_id"] = body };
        return Task.FromResult(properties);
    }
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
