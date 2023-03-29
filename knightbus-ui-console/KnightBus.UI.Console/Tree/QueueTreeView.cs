using KnightBus.UI.Console.Tree.Nodes;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace KnightBus.UI.Console.Tree;

public sealed class QueueTreeView : TreeView<QueueNode>
{
    private readonly ServiceBusQueueManager _queueManager;

    public QueueTreeView(ServiceBusQueueManager queueManager)
    {
        _queueManager = queueManager;
        TreeBuilder = new QueueTreeBuilder();
        this.KeyPress += OnKeyPress;
        this.SelectionChanged += GetSubQueues;
    }

    private void GetSubQueues(object sender, SelectionChangedEventArgs<QueueNode> e)
    {
        if(e.NewValue == null) return;
        if(e.NewValue.Properties?.Type != QueueType.Topic) return;

        var queues = _queueManager.ListSubscriptions(e.NewValue.Properties.Name, CancellationToken.None);

        foreach (var queue in queues)
        {
            e.NewValue.QueueNodes.Add(new SubscriptionNode(queue));
        }
    }

    private void OnKeyPress(KeyEventEventArgs obj)
    {
        if (obj.KeyEvent.Key == Key.DeleteChar && SelectedObject?.IsQueue == true)
        {
            DeleteQueue(SelectedObject);
            obj.Handled = true;
        }
        else if (obj.KeyEvent.Key == Key.F5 && SelectedObject?.IsQueue == true)
        {
            RefreshQueue(SelectedObject);
            obj.Handled = true;
        }
    }

    public void LoadQueues()
    {
        ClearObjects();
        var queues = _queueManager.List(CancellationToken.None).ToList();
        var topics = _queueManager.ListTopics(CancellationToken.None).ToList();
        var queueGroups = new Dictionary<string, List<QueueProperties>>();

        foreach (var q in queues)
        {
            var index = q.Name.IndexOf('-');
            var prefix = index == -1 ? q.Name : q.Name[..index];

            if (!queueGroups.ContainsKey(prefix))
            {
                queueGroups[prefix] = new List<QueueProperties>();
            }

            queueGroups[prefix].Add(q);
        }

        foreach (var q in topics)
        {
            var index = q.Name.IndexOf('-');
            var prefix = index == -1 ? q.Name : q.Name[..index];

            if (!queueGroups.ContainsKey(prefix))
            {
                queueGroups[prefix] = new List<QueueProperties>();
            }

            queueGroups[prefix].Add(q);
        }

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
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            if(queueGroupNode.QueueNodes.Any())
                serviceNode.QueueNodes.Add(queueGroupNode);
            if(topicGroupNode.QueueNodes.Any())
                serviceNode.QueueNodes.Add(topicGroupNode);

            AddObject(serviceNode);
        }
    }


    private void UpdateQueueNode(QueueNode node, QueueProperties q)
    {
        node.Update(q);
        RefreshObject(node);
        OnSelectionChanged(new SelectionChangedEventArgs<QueueNode>(this, node, node));
    }

    public void RefreshQueue(QueueNode node)
    {
        var name = node.Properties.Name;
        var q = _queueManager.Get(name, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        UpdateQueueNode(node, q);
    }

    public void DeleteQueue(QueueNode node)
    {
        if (MessageBox.Query($"Delete {node.Properties.Name}", "Are you sure?", "No", "Yes") == 1)
        {
            var name = node.Properties.Name;
            _queueManager.Delete(name, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            LoadQueues();
        }
    }
}
