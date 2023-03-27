namespace KnightBus.UI.Console.Tree.Nodes;

public class TopicNode : QueueNode
{
    public TopicNode(QueueProperties q) : base(q)
    {
        Text = CreateQueueLabel(q);
        IsQueue = false;
    }
}
