using Azure.Messaging.ServiceBus.Administration;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace KnightBus.UI.Console;

public sealed class QueueTreeView : TreeView
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
            var node = new TreeNode(queueGroup.Key);
            foreach (var q in queueGroup.Value)
            {
                var queueNode = CreateQueueNode(q);
                node.Children.Add(queueNode);
            }

            AddObject(node);
        }
    }


    private static TreeNode CreateQueueNode(QueueRuntimeProperties q)
    {
        var label = CreateQueueLabel(q);
        var queueNode = new TreeNode(label) { Tag = q };
        return queueNode;
    }

    private static string CreateQueueLabel(QueueRuntimeProperties q)
    {
        var index = q.Name.IndexOf('-');
        var queueName = index == -1 ? q.Name : q.Name[(index + 1)..];
        var label = $"{queueName} [{q.ActiveMessageCount},{q.DeadLetterMessageCount},{q.ScheduledMessageCount}]";
        return label;
    }

    private static void UpdateQueueNode(TreeView view, ITreeNode node, QueueRuntimeProperties q)
    {
        var label = CreateQueueLabel(q);
        node.Tag = q;
        node.Text = label;
        view.RefreshObject(node);
    }

    public void RefreshQueue(TreeView view, ITreeNode node)
    {
        var name = ((QueueRuntimeProperties)node.Tag).Name;
        var q = _queueManager.Get(name, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        UpdateQueueNode(view, node, q);
    }

    public void DeleteQueue(TreeView view, ITreeNode node)
    {
        var name = ((QueueRuntimeProperties)node.Tag).Name;
        _queueManager.Delete(name, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        LoadQueues();
    }
}
