using Terminal.Gui.Trees;

namespace KnightBus.UI.Console.Tree.Nodes;

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

    public bool IsQueue { get; protected set; }
    public QueueProperties Properties { get; set; }

    public override IList<ITreeNode> Children => QueueNodes.Cast<ITreeNode>().ToList();
    public sealed override string Text { get; set; }

    public void Update(QueueProperties properties)
    {
        Properties = properties;
        Text = CreateQueueLabel(properties);
        IsQueue = true;
    }

    protected static string CreateQueueLabel(QueueProperties q)
    {
        var index = q.Name.IndexOf('-');
        var queueName = index == -1 ? q.Name : q.Name[(index + 1)..];
        var label = $"{queueName} [{q.ActiveMessageCount},{q.DeadLetterMessageCount},{q.ScheduledMessageCount}]";
        return label;
    }
}
