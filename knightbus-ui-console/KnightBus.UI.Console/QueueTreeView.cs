using Azure.Messaging.ServiceBus.Administration;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace KnightBus.UI.Console;

public class QueueTreeBuilder : ITreeBuilder<QueueNode>
{
    public bool CanExpand(QueueNode toExpand)
    {
        return !toExpand.IsQueue;
    }

    public IEnumerable<QueueNode> GetChildren(QueueNode forObject)
    {
        return forObject.QueueNodes;
    }

    public bool SupportsCanExpand => true;
}

public class TopicNode : QueueNode
{
    public TopicNode(string label) : base(label)
    {
    }
}

public class QueueNode : TreeNode
{
    public readonly List<QueueNode> QueueNodes = new();

    public QueueNode(string label)
    {
        IsQueue = false;
        Text = label;
    }

    public QueueNode(QueueProperties properties)
    {
        Properties = properties;
        Text = CreateQueueLabel(properties);
        IsQueue = true;
    }

    public bool IsQueue { get; private set; }
    public QueueProperties Properties { get; set; }

    public override IList<ITreeNode> Children => QueueNodes.Cast<ITreeNode>().ToList();
    public sealed override string Text { get; set; }

    public void Update(QueueProperties properties)
    {
        Properties = properties;
        Text = CreateQueueLabel(properties);
        IsQueue = true;
    }

    private static string CreateQueueLabel(QueueProperties q)
    {
        var index = q.Name.IndexOf('-');
        var queueName = index == -1 ? q.Name : q.Name[(index + 1)..];
        var label = $"{queueName} [{q.ActiveMessageCount},{q.DeadLetterMessageCount},{q.ScheduledMessageCount}]";
        return label;
    }
}

public sealed class QueueTreeView : TreeView<QueueNode>
{
    private readonly ServiceBusQueueManager _queueManager;

    public QueueTreeView(ServiceBusQueueManager queueManager)
    {
        _queueManager = queueManager;
        TreeBuilder = new QueueTreeBuilder();
        this.KeyPress += OnKeyPress;
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

        foreach (var queueGroup in queueGroups)
        {
            var node = new QueueNode(queueGroup.Key);
            foreach (var q in queueGroup.Value)
            {
                var queueNode = new QueueNode(q);
                node.QueueNodes.Add(queueNode);
            }

            AddObject(node);
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
