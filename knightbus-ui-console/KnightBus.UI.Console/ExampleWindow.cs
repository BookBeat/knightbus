using System.Data;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.UI.Console;
using Terminal.Gui;
using Terminal.Gui.Trees;
using Attribute = Terminal.Gui.Attribute;

public sealed class ExampleWindow : Window
{
    public FrameView LeftPane;
    public TreeView QueueListView;
    public FrameView RightPane;
    public ListView ScenarioListView;
    private readonly ServiceBusQueueManager _queueManager;
    private List<QueueRuntimeProperties> _queues = new();
    private QueueRuntimeProperties _currentQueue;
    private int _messagesToMove;

    public ExampleWindow()
    {
        Title = "KnightBus Explorer (Ctrl+Q to quit)";

        ColorScheme = Colors.Base;
        MenuBar = new MenuBar(new MenuBarItem[]
        {
            new MenuBarItem("_File",
                new MenuItem[]
                {
                    new MenuItem("_Quit", "Quit KnightBus Explorer", RequestStop, null, null, Key.Q | Key.CtrlMask)
                }),
        });
        Add(MenuBar);
        LeftPane = new FrameView("Queues")
        {
            X = 0,
            Y = 1, // for menu
            Width = 35,
            Height = Dim.Fill(1),
            CanFocus = true,
            Shortcut = Key.CtrlMask | Key.C
        };
        LeftPane.Title = $"{LeftPane.Title} ({LeftPane.ShortcutTag})";
        LeftPane.ShortcutAction = () => LeftPane.SetFocus();

        Add(LeftPane);

        RightPane = new FrameView("Details")
        {
            X = 35,
            Y = 1, // for menu
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            CanFocus = true,
            Shortcut = Key.CtrlMask | Key.S
        };
        RightPane.Title = $"{RightPane.Title} ({RightPane.ShortcutTag})";
        RightPane.ShortcutAction = () => RightPane.SetFocus();

        Add(RightPane);


        QueueListView = new TreeView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(0),
            Height = Dim.Fill(0),
            CanFocus = true,

        };


        const string connection = "";

        _queueManager = new ServiceBusQueueManager(connection);

        LoadQueues();

        LeftPane.Add(QueueListView);
        QueueListView.SelectionChanged += QueueListViewOnSelectionChanged;
    }

    private void LoadQueues()
    {
        QueueListView.ClearObjects();
        _queues = _queueManager.List(CancellationToken.None).ToList();
        var queueGroups = new Dictionary<string, List<QueueRuntimeProperties>>();

        foreach (var q in _queues)
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

            QueueListView.AddObject(node);
        }
    }

    private static TreeNode CreateQueueNode(QueueRuntimeProperties q)
    {
        var index = q.Name.IndexOf('-');
        var queueName = index == -1 ? q.Name : q.Name[(index + 1)..];
        var label = $"{queueName} [{q.ActiveMessageCount},{q.DeadLetterMessageCount},{q.ScheduledMessageCount}]";
        var queueNode = new TreeNode(label) { Tag = q };
        return queueNode;
    }

    private void QueueListViewOnSelectionChanged(object sender, SelectionChangedEventArgs<ITreeNode> e)
    {
        RightPane.RemoveAll();
        if (e.NewValue?.Tag == null) return;
        //Show queue details


        _currentQueue = (QueueRuntimeProperties)e.NewValue.Tag;

        var queueTableView = new TableView(CreateTable(_currentQueue))
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(50),
        };

        RightPane.Add(queueTableView);

        if (_currentQueue.DeadLetterMessageCount < 1) return;
        var deadletter = _queueManager.PeekDeadLetter(_currentQueue.Name, 10, CancellationToken.None).GetAwaiter().GetResult();
        var deadletterLabel = new Label("Deadletters:")
        {
            X = 1,
            Y = Pos.Bottom(queueTableView) + 1,
        };

        RightPane.Add(deadletterLabel);
        var moveButton = new Button("Move Deadletter Messages")
        {
            X = 1,
            Y = Pos.Bottom(deadletterLabel) + 1,
        };
        moveButton.Clicked += MoveButtonOnClicked;
        RightPane.Add(moveButton);
        var deadLetterTableView = new TableView(CreateTable(deadletter))
        {
            Y = Pos.Bottom(moveButton),
            Width = Dim.Fill(),
            Height = Dim.Percent(50),
        };

        deadLetterTableView.CellActivated += DeadLetterTableViewOnCellActivated;
        RightPane.Add(deadLetterTableView);
    }

    private void MoveButtonOnClicked()
    {
        if (_currentQueue is null) return;

        var cancel = new Button("Cancel", true);
        var move = new Button("Move");
        cancel.Clicked += () => { Application.RequestStop(); };


        using var dialog = new Dialog(" Move Deadletter Messagers", cancel, move);
        var queueLabel = new Label(_currentQueue.Name)
        {
            X = Pos.Center(),
            Y = 1
        };

        var label = new Label("Number of messages to Move")
        {
            X = 1,
            Y = 3
        };
        var input = new TextField(_currentQueue.DeadLetterMessageCount.ToString()) { X = label.X, Y = label.Y + 1, Width = Dim.Width(label) };

        _messagesToMove = (int)_currentQueue.DeadLetterMessageCount;
        input.TextChanging += args =>
        {
            if (int.TryParse(args.NewText.ToString(), out var count))
            {
                _messagesToMove = count;
            }
            else
            {
                args.Cancel = true;
            }
        };

        move.Clicked += () =>
        {
            var result = MessageBox.Query($"Move {_messagesToMove} messages", "Are you sure?", "Yes", "No");
            if (result == 1) return;

            var count = _queueManager.MoveDeadLetters(_currentQueue.Name, _messagesToMove, CancellationToken.None).GetAwaiter().GetResult();

            MessageBox.Query("Complete", $"Moved {count} messages", "Ok");
            LoadQueues();
            Application.RequestStop();
        };

        dialog.Add(queueLabel);
        dialog.Add(label);
        dialog.Add(input);
        Application.Run(dialog);
    }

    private void DeadLetterTableViewOnCellActivated(TableView.CellActivatedEventArgs obj)
    {
        var ok = new Button("Ok", true);
        ok.Clicked += () => { Application.RequestStop(); };
        var dialog = new Dialog("Message Details", ok);
        var details = new TextView { X = 0, Y = 0, Width = Dim.Fill(1), Height = Dim.Fill(1) };

        var text = (string)obj.Table.Rows[obj.Row][1];
        details.Text = text;
        dialog.Add(details);
        Application.Run(dialog);
    }

    private DataTable CreateTable(QueueRuntimeProperties q)
    {
        var table = new DataTable();
        table.Columns.Add(q.Name);
        table.Columns.Add("Value");
        table.Rows.Add("ActiveMessageCount", q.ActiveMessageCount);
        table.Rows.Add("ScheduledMessageCount", q.ScheduledMessageCount);
        table.Rows.Add("DeadLetterMessageCount", q.DeadLetterMessageCount);
        table.Rows.Add("SizeInBytes", q.SizeInBytes);
        table.Rows.Add("TransferMessageCount", q.TransferMessageCount);
        table.Rows.Add("TransferDeadLetterMessageCount", q.TransferDeadLetterMessageCount);

        table.Rows.Add("CreatedAt", q.CreatedAt);
        table.Rows.Add("UpdatedAt", q.UpdatedAt);
        table.Rows.Add("AccessedAt", q.AccessedAt);
        return table;
    }
    private DataTable CreateTable(IEnumerable<ServiceBusReceivedMessage> messages)
    {
        var table = new DataTable();
        table.Columns.Add("Time");
        table.Columns.Add("Data");

        foreach (var message in messages)
        {
            table.Rows.Add(message.EnqueuedTime.ToString("s"), Encoding.UTF8.GetString(message.Body));
        }

        return table;
    }
}
