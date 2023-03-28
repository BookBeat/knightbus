using System.Data;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace KnightBus.UI.Console;

public sealed class MainWindow : Window
{
    public FrameView LeftPane;
    public QueueTreeView QueueListView;
    public FrameView RightPane;
    private readonly ServiceBusQueueManager _queueManager;
    private QueueRuntimeProperties _currentQueue;
    private int _messagesToMove;

    public MainWindow()
    {
        Title = "KnightBus Explorer (Ctrl+Q to quit)";

        ColorScheme = Colors.Base;
        MenuBar = new MenuBar(new[]
        {
            new MenuBarItem("_File",
                new[]
                {
                    new MenuItem("_Quit", "Quit KnightBus Explorer", RequestStop, null, null, Key.Q | Key.CtrlMask),
                    new MenuItem("_Refresh", "Refresh Queues", QueueListView.LoadQueues, null, null, Key.R | Key.CtrlMask)
                }),
            new MenuBarItem("_Queue",
                new[]
                {
                    new MenuItem("_Refresh", "Refresh Queue", () =>
                    {
                        QueueListView.RefreshQueue(QueueListView, QueueListView.SelectedObject);
                    }, () => _currentQueue != null, null),
                    new MenuItem("_Delete", "Delete Queue", () =>
                    {
                        if (MessageBox.Query($"Delete {_currentQueue.Name}", "Are you sure?", "No", "Yes") == 1)
                        {
                            QueueListView.DeleteQueue(QueueListView, QueueListView.SelectedObject);
                        }
                    }, () => _currentQueue != null, null)
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

        const string connection = "";
        _queueManager = new ServiceBusQueueManager(connection);

        QueueListView = new QueueTreeView(_queueManager)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(0),
            Height = Dim.Fill(0),
            CanFocus = true,

        };




        QueueListView.LoadQueues();

        LeftPane.Add(QueueListView);
        QueueListView.SelectionChanged += QueueListViewOnSelectionChanged;
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
            QueueListView.RefreshQueue(QueueListView, QueueListView.SelectedObject);
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
