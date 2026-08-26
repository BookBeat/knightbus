using FluentAssertions;
using KnightBus.Core;
using KnightBus.PostgreSql.Management;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

public class PostgresMessageStateHandlerTests : MessageStateHandlerTests<PostgresTestCommand>
{
    private PostgresBus _bus;
    private PostgresQueueClient<PostgresTestCommand> _postgresQueueClient;
    private PostgresManagementClient _postgresManagementClient;

    public override async Task Setup()
    {
        _postgresManagementClient = new PostgresManagementClient(
            PostgresSetup.DataSource,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() }
        );
        _postgresQueueClient = new PostgresQueueClient<PostgresTestCommand>(
            PostgresSetup.DataSource,
            new MicrosoftJsonSerializer()
        );
        _bus = new PostgresBus(
            PostgresSetup.DataSource,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() },
            []
        );

        await QueueInitializer.InitQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()),
            PostgresSetup.DataSource
        );
    }

    [TearDown]
    public async Task CleanUpAfterTests()
    {
        await _postgresManagementClient.DeleteQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()),
            default
        );
    }

    protected override async Task<List<PostgresTestCommand>> GetMessages(int count)
    {
        var messages = _postgresQueueClient.GetMessagesAsync(count, 0, default);
        var result = new List<PostgresTestCommand>();
        await foreach (var m in messages)
        {
            result.Add(m.Message);
        }

        return result;
    }

    protected override async Task<List<PostgresTestCommand>> GetDeadLetterMessages(int count)
    {
        var messages = _postgresQueueClient.PeekDeadLetterMessagesAsync(count, default);
        var result = new List<PostgresTestCommand>();
        await foreach (var m in messages)
        {
            result.Add(m.Message);
        }

        return result;
    }

    protected override async Task SendMessage(string message)
    {
        await QueueInitializer.InitQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()),
            PostgresSetup.DataSource
        );
        await _bus.SendAsync(new PostgresTestCommand(message), default);
    }

    protected override async Task<
        IMessageStateHandler<PostgresTestCommand>
    > GetMessageStateHandler()
    {
        return await GetMessageStateHandler(visibilityTimeoutSeconds: 5);
    }

    [Test]
    public async Task Should_extend_the_lock_and_keep_the_message_invisible()
    {
        //arrange
        await SendMessage("Testing Lock Extension");
        var stateHandler = await GetMessageStateHandler(visibilityTimeoutSeconds: 0);

        //act
        await stateHandler.SetLockDuration(TimeSpan.FromSeconds(30), default);

        //assert
        var messages = await GetMessages(1);
        messages.Should().BeEmpty();
    }

    [Test]
    public async Task Should_not_extend_a_lock_that_another_consumer_has_taken_over()
    {
        //arrange
        await SendMessage("Testing Stale Lock Extension");
        var firstConsumer = await GetMessageStateHandler(visibilityTimeoutSeconds: 0);
        await GetMessageStateHandler(visibilityTimeoutSeconds: 0);

        //act
        await firstConsumer.SetLockDuration(TimeSpan.FromSeconds(30), default);

        //assert
        var messages = await GetMessages(1);
        messages.Should().HaveCount(1);
    }

    [Test]
    public async Task Should_ignore_a_lock_extension_after_the_message_was_completed()
    {
        //arrange
        await SendMessage("Testing Extension After Complete");
        var stateHandler = await GetMessageStateHandler(visibilityTimeoutSeconds: 0);
        await stateHandler.CompleteAsync();

        //act
        await stateHandler
            .Awaiting(h => h.SetLockDuration(TimeSpan.FromSeconds(30), default))
            .Should()
            .NotThrowAsync();

        //assert
        var messages = await GetMessages(1);
        messages.Should().BeEmpty();
        var deadLetters = await GetDeadLetterMessages(1);
        deadLetters.Should().BeEmpty();
    }

    private async Task<PostgresMessageStateHandler<PostgresTestCommand>> GetMessageStateHandler(
        int visibilityTimeoutSeconds
    )
    {
        var messages = _postgresQueueClient.GetMessagesAsync(1, visibilityTimeoutSeconds, default);
        var result = new List<PostgresMessage<PostgresTestCommand>>();
        await foreach (var m in messages)
        {
            result.Add(m);
        }

        return new PostgresMessageStateHandler<PostgresTestCommand>(
            PostgresSetup.DataSource,
            _postgresQueueClient,
            result.First(),
            5,
            new MicrosoftJsonSerializer(),
            null!
        );
    }
}
