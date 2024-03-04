using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.UI.Blazor.Providers;
using KnightBus.UI.Blazor.Providers.ServiceBus;

namespace KnightBus.UI.Blazor.Data;

public class TopicsService
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusTopicManager _serviceBusTopicManager;

    public TopicsService(EnvironmentService environmentService, IServiceProvider provider,
        ServiceBusTopicManager serviceBusTopicManager)
    {
        var configuration = provider.GetRequiredKeyedService<EnvironmentConfig>(environmentService.Get());
        _adminClient = new ServiceBusAdministrationClient(configuration.ServiceBusConnectionString);
        _client = new ServiceBusClient(configuration.ServiceBusConnectionString);
        _serviceBusTopicManager = serviceBusTopicManager;
    }

    public async Task<IList<SubscriptionNode>> ListTopicSubscriptions(CancellationToken ct)
    {
        var subscriptionNodes = new List<SubscriptionNode>();
        var topics = await _serviceBusTopicManager.List(ct);
        foreach (var topic in topics)
        {
            var subscriptions = await topic.Manager.List(ct);
            subscriptionNodes.AddRange(
                subscriptions.Select(subscription => new SubscriptionNode((SubscriptionQueueProperties)subscription)));
        }

        return subscriptionNodes;
    }


    public async Task<SubscriptionQueueProperties> GetSubscription(
        string topic, string subscription)
    {
        var queueManager = new ServiceBusSubscriptionManager(topic, _client, _adminClient);

        return (SubscriptionQueueProperties)await queueManager.Get(subscription, CancellationToken.None);
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(
        string topic, string subscription, int count)
    {
        var queueManager = new ServiceBusSubscriptionManager(topic, _client, _adminClient);

        var messages = await queueManager.PeekDeadLetter(subscription, count, CancellationToken.None);
        return messages;
    }

    public async Task<IReadOnlyList<QueueMessage>> ReceiveDeadLetter(
        string topic, string subscription, int count)
    {
        var queueManager = new ServiceBusSubscriptionManager(topic, _client, _adminClient);

        var messages = await queueManager.ReadDeadLetter(subscription, count, CancellationToken.None);
        return messages;
    }

    public async Task MoveDeadLetters(
        string topic, string subscription, int count)
    {
        var queueManager = new ServiceBusSubscriptionManager(topic, _client, _adminClient);

        await queueManager.MoveDeadLetters(subscription, count, CancellationToken.None);
    }

}
