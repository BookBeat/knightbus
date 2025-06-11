using System.Collections.Concurrent;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Cosmos.Messages;
using KnightBus.Host;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Cosmos.Tests.Integration;

[TestFixture]
class ProcessingTests : CosmosTestBase
{
    [TearDown]
    public void TearDown()
    {
        ProcessedTracker.processed = new ConcurrentDictionary<string, int>();
    }

    //Message bodies must be unique to keep track of processing
    [Test]
    public async Task AllCommandsProcessed()
    {
        const int numMessages = 1000;
        SampleCosmosCommand[] messages = new SampleCosmosCommand[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new SampleCosmosCommand() { MessageBody = $"msg data {i}" };
            messageContents[i] = messages[i].MessageBody;
        }
        await _publisher.SendAsync(messages, CancellationToken.None);
        await ProcessedTracker.WaitForProcessedMessagesAsync(messageContents, numMessages);
        ProcessedTracker.Processed(messageContents).Should().Be(numMessages);
    }

    //Message bodies must be unique to keep track of processing
    [Test]
    public async Task AllEventsProcessedWhenOneSubscriber()
    {
        const int numMessages = 100;

        OneSubCosmosEvent[] messages = new OneSubCosmosEvent[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new OneSubCosmosEvent() { MessageBody = $"msg data {i}" };
            messageContents[i] = messages[i].MessageBody;
        }

        await _publisher.PublishAsync(messages, CancellationToken.None);
        await ProcessedTracker.WaitForProcessedMessagesAsync(messageContents, numMessages);
        ProcessedTracker.Processed(messageContents).Should().Be(numMessages);
    }

    //Message bodies must be unique to keep track of processing
    [Test]
    public async Task AllEventsProcessedWhenToTwoSubscribers()
    {
        const int numMessages = 100;

        TwoSubCosmosEvent[] messages = new TwoSubCosmosEvent[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new TwoSubCosmosEvent() { MessageBody = $"msg data {i}" };
            messageContents[i] = messages[i].MessageBody;
        }
        await _publisher.PublishAsync(messages, CancellationToken.None);
        await ProcessedTracker.WaitForProcessedMessagesAsync(messageContents, numMessages, 2);
        ProcessedTracker.Processed(messageContents, 2).Should().Be(numMessages);
    }

    //Message bodies must be unique to keep track of processing
    [Test]
    public async Task FailedEventsShouldBeRetriedAccordingToDeadLetterLimit()
    {
        const int numMessages = 100;

        PoisonEvent[] messages = new PoisonEvent[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new PoisonEvent() { Body = $"msg data {i}" };
            messageContents[i] = messages[i].Body;
        }
        var settings = new CosmosProcessingSetting();
        int deadLetterLimit = settings.DeadLetterDeliveryLimit;

        await _publisher.PublishAsync(messages, CancellationToken.None);
        await ProcessedTracker.WaitForProcessedMessagesAsync(
            messageContents,
            numMessages,
            deadLetterLimit,
            10
        );
        //Processed denotes times processing an event has started
        ProcessedTracker.Processed(messageContents, deadLetterLimit).Should().Be(numMessages);
    }

    //Message bodies must be unique to keep track of processing
    //Test might fail if previous execution of test failed
    [Test]
    public async Task EventsShouldBeProcessedOrDeadLettered()
    {
        const int numMessages = 100;

        SporadicErrorEvent[] messages = new SporadicErrorEvent[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new SporadicErrorEvent() { Body = $"{i}" };
            messageContents[i] = messages[i].Body;
        }

        await _publisher.PublishAsync(messages, CancellationToken.None);
        await ProcessedTracker.WaitForProcessedMessagesAsync(messageContents, numMessages);
        //Processed denotes times processing an event has completed
        int processed = ProcessedTracker.Processed(messageContents);

        //Get deadlettered events
        var connectionString = Environment.GetEnvironmentVariable("CosmosString");
        CosmosClient cosmosClient = new CosmosClient(connectionString);
        Container container = cosmosClient.GetContainer(
            "PubSub",
            $"{AutoMessageMapper.GetQueueName<SporadicErrorEvent>()}_DL"
        );
        var query = container.GetItemQueryIterator<SporadicErrorEvent>("SELECT * FROM c");
        int deadletters = 0;

        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            deadletters += response.Count;
        }

        //Remove container after test to prevent deadlettered events impacting future tests
        await container.DeleteContainerAsync();

        (processed + deadletters).Should().Be(numMessages);
    }
}
