using KnightBus.UI.Console.Providers;
using KnightBus.UI.Console.Tree.Nodes;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace KnightBus.UI.Console.Tree;

public sealed class QueueTreeView : TreeView<QueueNode>
{
    private const string Unknown = "Unknown";
    private readonly IQueueManager[] _queueManager;

    public QueueTreeView(IQueueManager[] queueManager)
    {
        _queueManager = queueManager;
        TreeBuilder = new QueueTreeBuilder();
        this.KeyPress += OnKeyPress;
        this.SelectionChanged += GetSubQueues;
        this.SelectionChanged += LoadChildQueueProperties;
    }

    private void LoadChildQueueProperties(object sender, SelectionChangedEventArgs<QueueNode> e)
    {
        if (e.NewValue == null) return;
        if (e.NewValue.IsQueue) return;

        foreach (var childNode in e.NewValue.QueueNodes.Where(n => n.IsQueue && n.Properties?.IsLoaded == false))
        {
            var updated = childNode.Properties.Manager.Get(childNode.Properties.Name, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            childNode.Update(updated);
        }
    }

    private void GetSubQueues(object sender, SelectionChangedEventArgs<QueueNode> e)
    {
        if (e.NewValue == null) return;
        if (e.NewValue.Properties?.HasSubQueues != true) return;

        var queues = e.NewValue.Properties.Manager.List(CancellationToken.None);

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
        var queueGroups = new Dictionary<string, List<QueueProperties>>();
        foreach (var manager in _queueManager)
        {
            LoadQueues(manager, queueGroups);
        }

        var found = new List<QueueProperties>();
        foreach (var queueProp in queueGroups.Single(g => g.Key == Unknown).Value)
        {
            foreach (var queueGroup in queueGroups.Where(queueGroup => queueProp.Name.StartsWith(queueGroup.Key, StringComparison.OrdinalIgnoreCase)))
            {
                queueGroup.Value.Add(queueProp);
                found.Add(queueProp);
            }
        }

        foreach (var foundProp in found)
        {
            queueGroups[Unknown].Remove(foundProp);
            if (!queueGroups[Unknown].Any())
                queueGroups.Remove(Unknown);
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

            if (queueGroupNode.QueueNodes.Any())
                serviceNode.QueueNodes.Add(queueGroupNode);
            if (topicGroupNode.QueueNodes.Any())
                serviceNode.QueueNodes.Add(topicGroupNode);

            AddObject(serviceNode);
        }
    }
    private void LoadQueues(IQueueManager manager, Dictionary<string, List<QueueProperties>> queueGroups)
    {
        var queues = manager.List(CancellationToken.None);
        foreach (var q in queues)
        {
            var index = q.Name.IndexOf('-');
            var prefix = index == -1 ? Unknown : q.Name[..index];

            if (!queueGroups.ContainsKey(prefix))
            {
                queueGroups[prefix] = new List<QueueProperties>();
            }

            queueGroups[prefix].Add(q);
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
        var q = node.Properties.Manager.Get(name, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        UpdateQueueNode(node, q);
    }

    public void DeleteQueue(QueueNode node)
    {
        if (MessageBox.Query($"Delete {node.Properties.Name}", "Are you sure?", "No", "Yes") == 1)
        {
            var name = node.Properties.Name;
            node.Properties.Manager.Delete(name, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            LoadQueues();
        }
    }
}
