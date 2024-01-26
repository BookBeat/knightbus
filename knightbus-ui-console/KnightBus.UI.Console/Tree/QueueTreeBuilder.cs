using KnightBus.UI.Console.Tree.Nodes;
using Terminal.Gui.Trees;

namespace KnightBus.UI.Console.Tree;

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
