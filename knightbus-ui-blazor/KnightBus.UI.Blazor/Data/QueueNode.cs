using KnightBus.UI.Blazor.Providers;

namespace KnightBus.UI.Blazor.Data;

public class QueueNode
{
    public QueueNode(string label)
    {
        Label = label;
        IsQueue = false;
    }

    public QueueNode(QueueProperties properties)
    {
        Properties = properties;
        Label = CreateQueueLabel(properties);
        IsQueue = true;
    }

    public QueueProperties Properties { get; } = null!;

    public string Label { get; set; }
    public bool IsQueue { get; set; }
    public List<QueueNode> QueueNodes { get; set; } = new();

    public long DeadLetterMessageCount => Properties?.DeadLetterMessageCount ?? 0;

    protected static string CreateQueueLabel(QueueProperties q)
    {
        var index = q.Name.IndexOf('-');
        var queueName = index == -1 ? q.Name : q.Name[(index + 1)..];
        var label = $"{queueName} [{q.ActiveMessageCount},{q.DeadLetterMessageCount},{q.ScheduledMessageCount}]";
        return label;
    }

    public virtual string Url => $"details/{Properties.Manager.DisplayName}/{Properties.Name}";
}


public class TopicNode : QueueNode
{
    public TopicNode(QueueProperties q) : base(q)
    {
        Label = q.Name;
        IsQueue = false;
    }

    public override string Url => "";
}


public class SubscriptionNode : QueueNode
{
    private readonly string _topic;

    public string Topic => _topic;

    public SubscriptionNode(SubscriptionQueueProperties q) : base(q)
    {
        _topic = q.Topic;
    }

    public override string Url => $"topics/{_topic}/{Properties.Name}";
}
