using KnightBus.UI.Blazor.Providers;
using KnightBus.UI.Blazor.Providers.ServiceBus;
using KnightBus.UI.Blazor.Providers.StorageBus;

namespace KnightBus.UI.Blazor.Data;

public class QueueService
{
    private readonly IEnumerable<IQueueManager> _queueManagers;
    private readonly ServiceBusQueueManager _serviceBusQueueManager;
    private readonly ServiceBusTopicManager _serviceBusTopicManager;
    private readonly StorageQueueManager _storageQueueManager;

    public QueueService(
        IEnumerable<IQueueManager> queueManagers,
        ServiceBusQueueManager serviceBusQueueManager,
        ServiceBusTopicManager serviceBusTopicManager,
        StorageQueueManager storageQueueManager)
    {
        _queueManagers = queueManagers;
        _serviceBusQueueManager = serviceBusQueueManager;
        _serviceBusTopicManager = serviceBusTopicManager;
        _storageQueueManager = storageQueueManager;
    }

    public async Task<QueueProperties> GetQueue(
        string path,
        string manager)
    {
        var queueManager = _queueManagers.First(m => m.DisplayName == manager);
        return await queueManager.Get(path, CancellationToken.None);
    }

    public async Task<IReadOnlyList<QueueMessage>> Peek(
        string name,
        string manager,
        int count)
    {
        var queueManager = _queueManagers.First(m => m.DisplayName == manager);
        return await queueManager.Peek(
            name,
            count,
            CancellationToken.None);
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(
        string name,
        string manager,
        int count)
    {
        var queueManager = _queueManagers.First(m => m.DisplayName == manager);
        var messages = await queueManager.PeekDeadLetter(
            name,
            count,
            CancellationToken.None);
        return messages;
    }

    public async Task<IReadOnlyList<QueueMessage>> ReceiveDeadLetter(
        string name,
        string manager,
        int count)
    {
        var queueManager = _queueManagers.First(m => m.DisplayName == manager);
        var messages = await queueManager.ReadDeadLetter(
            name,
            count,
            CancellationToken.None);
        return messages;
    }

    public async Task MoveDeadLetters(
        string name,
        string manager,
        int count)
    {
        var queueManager = _queueManagers.First(m => m.DisplayName == manager);
        await queueManager.MoveDeadLetters(
            name,
            count,
            CancellationToken.None);
    }

    public async Task<IEnumerable<QueueNode>> GetServiceBusQueues(CancellationToken ct)
    {
        var queues = await _serviceBusQueueManager.List(ct);
        return queues.Select(q => new QueueNode(q));
    }

    public async Task<IEnumerable<QueueNode>> GetStorageQueues(CancellationToken ct)
    {
        var queues = await _storageQueueManager.List(ct);
        return queues.Select(q => new QueueNode(q));
    }


    public async Task<IEnumerable<QueueNode>> GetServiceBusNodes(CancellationToken ct)
    {
        var queueGroups = new Dictionary<string, List<QueueProperties>>();

        foreach (var queueManager in new List<IQueueManager>
                 {
                     _serviceBusQueueManager,
                     _serviceBusTopicManager
                 })
        {
            await LoadQueues(queueManager, queueGroups);
        }

        return ToQueueNodes(queueGroups);
    }

    public async Task<IEnumerable<QueueNode>> GetStorageQueueNodes(CancellationToken ct)
    {
        var queueGroups = new Dictionary<string, List<QueueProperties>>();

        foreach (var queueManager in new List<IQueueManager>
                 {
                     _storageQueueManager
                 })
        {
            await LoadQueues(queueManager, queueGroups);
        }

        var nodes = new List<QueueNode>();
        foreach (var queueGroup in queueGroups)
        {
            var serviceNode = new QueueNode(queueGroup.Key);
            foreach (var q in queueGroup.Value)
            {
                serviceNode.QueueNodes.Add(new QueueNode(q));
            }

            nodes.Add(serviceNode);
        }

        return nodes;
    }

    private IEnumerable<QueueNode> ToQueueNodes(Dictionary<string, List<QueueProperties>> queueGroups)
    {
        var nodes = new List<QueueNode>();
        foreach (var queueGroup in queueGroups)
        {
            var serviceNode = new QueueNode(queueGroup.Key);
            var queueGroupNode = new QueueNode("Queues");
            var topicGroupNode = new QueueNode("Topics");
            foreach (var q in queueGroup.Value)
            {
                QueueNode queueNode;
                switch (q.Type)
                {
                    case QueueType.Queue:
                        queueNode = new QueueNode(q);
                        queueGroupNode.QueueNodes.Add(queueNode);
                        break;
                    case QueueType.Topic:
                        queueNode = new TopicNode(q);
                        topicGroupNode.QueueNodes.Add(queueNode);
                        break;
                    case QueueType.Subscription:
                        var subNode = new SubscriptionNode((SubscriptionQueueProperties)q);
                        topicGroupNode.QueueNodes.First(n => n.Properties.Name == subNode.Topic).QueueNodes.Add(subNode);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (queueGroupNode.QueueNodes.Any())
                serviceNode.QueueNodes.Add(queueGroupNode);
            if (topicGroupNode.QueueNodes.Any())
                serviceNode.QueueNodes.Add(topicGroupNode);

            nodes.Add(serviceNode);
        }

        return nodes;
    }

    private async Task LoadQueues(IQueueManager manager, IDictionary<string, List<QueueProperties>> queueGroups)
    {
        var queues = await manager.List(CancellationToken.None);
        foreach (var q in queues)
        {
            if (!queueGroups.ContainsKey(q.QueueGroup))
            {
                queueGroups[q.QueueGroup] = [];
            }

            queueGroups[q.QueueGroup].Add(q);

            if (q.HasSubQueues)
            {
                var subQueues = await q.Manager.List(CancellationToken.None);
                foreach (var subQueue in subQueues)
                {
                    queueGroups[q.QueueGroup].Add(subQueue);
                }
            }
        }
    }
}
