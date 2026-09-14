using KnightBus.PostgreSql.Management;
using Npgsql;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Unit;

[TestFixture]
public class PostgresTopicManagerTests
{
    private PostgresTopicManager _topicManager = null!;

    [SetUp]
    public void Setup()
    {
        // No connection is ever opened, the topic name is rejected before any SQL is built.
        var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=knightbus");
        var configuration = new PostgresConfiguration();
        _topicManager = new PostgresTopicManager(
            new PostgresManagementClient(dataSource, configuration),
            configuration
        );
    }

    [TestCase("topic;DROP TABLE knightbus.metadata;--")]
    [TestCase("topic\";DROP TABLE knightbus.metadata;--")]
    [TestCase("my-topic")]
    [TestCase("")]
    public void Get_should_reject_topic_names_that_are_not_valid_identifiers(string topic)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _topicManager.Get(topic, CancellationToken.None)
        );
    }
}
