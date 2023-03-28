using Azure.Messaging.ServiceBus.Administration;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace KnightBus.UI.Console;

public class QueueNode : TreeNode
{
    public List<QueueNode> QueueNodes = new();
    public QueueNode(string label)
    {
        IsQueue = false;
        Text = label;
    }
    public QueueNode(QueueRuntimeProperties properties)
    {
        Properties = properties;
        Text = properties.Name;
        IsQueue = true;
    }
    public bool IsQueue { get; }
    public QueueRuntimeProperties Properties { get; }

    public override IList<ITreeNode> Children => QueueNodes.Cast<ITreeNode>().ToList();
    public sealed override string Text { get; set; }
}

public sealed class QueueTreeView : TreeView<QueueNode>
{
    private readonly ServiceBusQueueManager _queueManager;

    public QueueTreeView(ServiceBusQueueManager queueManager)
    {
        _queueManager = queueManager;
    }
    public void LoadQueues()
    {
        ClearObjects();
        var queues = _queueManager.List(CancellationToken.None).ToList();
        var queueGroups = new Dictionary<string, List<QueueRuntimeProperties>>();

        foreach (var q in queues)
        {
            var index = q.Name.IndexOf('-');
            var prefix = index == -1 ? q.Name : q.Name[..index];

            if (!queueGroups.ContainsKey(prefix))
            {
                queueGroups[prefix] = new List<QueueRuntimeProperties>();
            }

            queueGroups[prefix].Add(q);
        }

        foreach (var queueGroup in queueGroups)
        {
            var node = new QueueNode(queueGroup.Key);
            foreach (var q in queueGroup.Value)
            {
                var queueNode = CreateQueueNode(q);
                node.Children.Add(queueNode);
            }

            AddObject(node);
        }
    }


    private static QueueNode CreateQueueNode(QueueRuntimeProperties q)
    {
        var queueNode = new QueueNode(q);
        return queueNode;
    }

    private static string CreateQueueLabel(QueueRuntimeProperties q)
    {
        var index = q.Name.IndexOf('-');
        var queueName = index == -1 ? q.Name : q.Name[(index + 1)..];
        var label = $"{queueName} [{q.ActiveMessageCount},{q.DeadLetterMessageCount},{q.ScheduledMessageCount}]";
        return label;
    }

    private void UpdateQueueNode(QueueNode node, QueueRuntimeProperties q)
    {
        var label = CreateQueueLabel(q);
        node.Text = label;
        RefreshObject(node);
    }

    public void RefreshQueue(QueueNode node)
    {
        var name = node.Properties.Name;
        var q = _queueManager.Get(name, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        UpdateQueueNode(node, q);
    }

    public void DeleteQueue(QueueNode node)
    {
        var name = node.Properties.Name;
        _queueManager.Delete(name, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        LoadQueues();
    }
}
