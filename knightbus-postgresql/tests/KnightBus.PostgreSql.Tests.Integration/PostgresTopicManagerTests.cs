using FluentAssertions;
using KnightBus.Core;
using KnightBus.PostgreSql.Management;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

[TestFixture]
public class PostgresTopicManagerTests
{
    private PostgresManagementClient _postgresManagementClient;
    private readonly PostgresTestEventSubscription _eventSubscription = new();
    private PostgresTopicManager _queueManager;

    [SetUp]
    public async Task Setup()
    {
        var config = new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() };
        _postgresManagementClient = new PostgresManagementClient(PostgresSetup.DataSource,config);
        _queueManager = new PostgresTopicManager(_postgresManagementClient, config);
        await QueueInitializer.InitSubscription(PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestEvent>()), PostgresQueueName.Create(_eventSubscription.Name), PostgresSetup.DataSource);
    }

    [Test]
    public async Task Should_list_subscription()
    {
        var topics = await _queueManager.List(CancellationToken.None);
        topics.Count().Should().Be(1);
        topics.First().Name.Should().Be(AutoMessageMapper.GetQueueName<PostgresTestEvent>());
    }
}
