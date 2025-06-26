using FluentAssertions;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos.Tests.Integration;

public class StateTests : CosmosTestBase
{
    private CosmosClient _client;

    [OneTimeSetUp]
    public void CosmosClientSetup()
    {
        string connectionString = Environment.GetEnvironmentVariable("CosmosString");
        _client = new CosmosClient(connectionString);
    }

    [Test]
    public async Task EventInsertedIntoTopicContainer()
    {
        const int numMessages = 100;

        OneSubCosmosEvent[] messages = new OneSubCosmosEvent[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new OneSubCosmosEvent() { MessageBody = $"msg data {i}" };
        }

        //Get items existing in the topic container before publishing
        string topicContainer = AutoMessageMapper.GetQueueName<OneSubCosmosEvent>();
        var existing = await GetItemsFromContainerAsync<OneSubCosmosEvent>(topicContainer);

        await Publisher.PublishAsync(messages, CancellationToken.None);

        //Filter items existing prior to publishing and convert from internalEvent to Event
        var existingPostPublish = await GetItemsFromContainerAsync<OneSubCosmosEvent>(
            topicContainer
        );
        var existingBodies = new HashSet<string>(existing.Select(e => e.id));
        var newEvents = existingPostPublish.Where(e => !existingBodies.Contains(e.id)).ToList();
        var newEventsExternal = newEvents.Select(x => x.Message).ToList();

        //New events on topic container should equal published events
        newEventsExternal.Should().BeEquivalentTo(messages);
    }

    //Test requires the message bodies to be unique
    [Test]
    public async Task EventInSubscriberContainerWhenFailing()
    {
        const int numMessages = 100;
        ProcessedTracker.processed.Clear();

        FailFirstEvent[] messages = new FailFirstEvent[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new FailFirstEvent() { Body = $"msg data {i}" };
        }

        //Get items existing in the topic container before publishing
        string subContainer = $"{AutoMessageMapper.GetQueueName<FailFirstEvent>()}_Retry_FFSub";
        var existing = await GetItemsFromContainerAsync<FailFirstEvent>(subContainer);

        await Publisher.PublishAsync(messages, CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1));
        //Filter items existing prior to publishing and convert from internalEvent to Event
        var existingPostPublish = await GetItemsFromContainerAsync<FailFirstEvent>(subContainer);
        var existingBodies = new HashSet<string>(existing.Select(e => e.id));
        var newEvents = existingPostPublish.Where(e => !existingBodies.Contains(e.id)).ToList();
        var newEventsExternal = newEvents.Select(x => x.Message).ToList();

        //New events on topic container should equal published events
        newEventsExternal.Should().BeEquivalentTo(messages);
    }

    //Returns a list of internalItems in container
    public async Task<List<InternalCosmosMessage<T>>> GetItemsFromContainerAsync<T>(
        string containerName
    )
        where T : ICosmosEvent
    {
        Container container = _client.GetContainer(_databaseId, containerName);

        var query = container.GetItemQueryIterator<InternalCosmosMessage<T>>("SELECT * FROM c");
        List<InternalCosmosMessage<T>> results = new List<InternalCosmosMessage<T>>();
        while (query.HasMoreResults)
        {
            FeedResponse<InternalCosmosMessage<T>> response = await query.ReadNextAsync();
            results.AddRange(response.Resource);
        }
        return results;
    }
}
