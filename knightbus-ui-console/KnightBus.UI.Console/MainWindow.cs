using System.Data;
using System.Text;
using Azure.Messaging.ServiceBus;
using KnightBus.UI.Console.Providers;
using KnightBus.UI.Console.Tree;
using KnightBus.UI.Console.Tree.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace KnightBus.UI.Console;

public sealed class MainWindow : Window
{
    private readonly IServiceProvider _provider;
    public FrameView LeftPane;
    public QueueTreeView QueueListView;
    public FrameView RightPane;

    public MainWindow(IServiceProvider provider)
    {
        _provider = provider;
        Title = "KnightBus Explorer (Ctrl+Q to quit)";
        ColorScheme = Colors.Base;

        var managers = provider.GetServices<IQueueManager>();

        QueueListView = new QueueTreeView(managers.ToArray())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(0),
            Height = Dim.Fill(0),
            CanFocus = true,
            AutoSize = true
        };

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
                    new MenuItem("_Refresh", "Refresh Queue", () => { QueueListView.RefreshQueue(QueueListView.SelectedObject); }, () => QueueListView.SelectedObject?.IsQueue == true),
                    new MenuItem("_Move Deadletters", "Move Deadletter Messages", MoveDeadletterMessages, () => QueueListView.SelectedObject?.IsQueue == true, null, Key.M | Key.CtrlMask),
                    new MenuItem("_Delete", "Delete Queue", () => { QueueListView.DeleteQueue(QueueListView.SelectedObject); }, () => QueueListView.SelectedObject?.IsQueue == true)
                }),
        });
        Add(MenuBar);
        LeftPane = new FrameView("Queues")
        {
            X = 0,
            Y = 1, // for menu
            Width = Dim.Percent(30),
            Height = Dim.Fill(1),
            CanFocus = true,
            Shortcut = Key.CtrlMask | Key.C,
            AutoSize = true,
            LayoutStyle = LayoutStyle.Computed
        };
        LeftPane.Title = $"{LeftPane.Title} ({LeftPane.ShortcutTag})";
        LeftPane.ShortcutAction = () => LeftPane.SetFocus();

        Add(LeftPane);

        RightPane = new FrameView("Details")
        {
            X = Pos.Right(LeftPane) + 1,
            Y = 1, // for menu
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            CanFocus = true,
            Shortcut = Key.CtrlMask | Key.S
        };
        RightPane.Title = $"{RightPane.Title} ({RightPane.ShortcutTag})";
        RightPane.ShortcutAction = () => RightPane.SetFocus();

        Add(RightPane);
        QueueListView.LoadQueues();

        LeftPane.Add(QueueListView);
        QueueListView.SelectionChanged += QueueListViewOnSelectionChanged;
    }


    private void QueueListViewOnSelectionChanged(object sender, SelectionChangedEventArgs<QueueNode> e)
    {
        RightPane.RemoveAll();
        if (e.NewValue?.IsQueue != true) return;
        //Show queue details

        var queueTableView = new TableView(CreateTable(QueueListView.SelectedObject.Properties))
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(50),
        };

        RightPane.Add(queueTableView);

        var peekButton = new Button("Peek Deadletter Messages")
        {
            X = 1,
            Y = Pos.Bottom(queueTableView) + 1,
        };
        RightPane.Add(peekButton);
        peekButton.Clicked += async delegate
        {
            var deadletters = await QueueListView.SelectedObject.Properties.Manager.PeekDeadLetter(QueueListView.SelectedObject.Properties.Name, 10, CancellationToken.None);
            var deadLetterTableView = new TableView(CreateTable(deadletters))
            {
                Y = Pos.Bottom(peekButton),
                Width = Dim.Fill(),
                Height = Dim.Percent(50),
            };

            deadLetterTableView.CellActivated += DeadLetterTableViewOnCellActivated;
            RightPane.Add(deadLetterTableView);
        };


        var moveButton = new Button("Move Deadletter Messages")
        {
            X = Pos.Right(peekButton) + 1,
            Y = Pos.Bottom(queueTableView) + 1,
        };
        moveButton.Clicked += MoveDeadletterMessages;
        RightPane.Add(moveButton);
    }



    private void MoveDeadletterMessages()
    {
        if (QueueListView.SelectedObject?.IsQueue != true) return;

        var cancel = new Button("Cancel", true);
        var move = new Button("Move");
        cancel.Clicked += () => { Application.RequestStop(); };


        using var dialog = new Dialog(" Move Deadletter Messagers", cancel, move);
        var queueLabel = new Label(QueueListView.SelectedObject.Properties.Name)
        {
            X = Pos.Center(),
            Y = 1
        };

        var label = new Label("Number of messages to Move")
        {
            X = 1,
            Y = 3
        };
        var input = new TextField(QueueListView.SelectedObject.Properties.DeadLetterMessageCount.ToString()) { X = label.X, Y = label.Y + 1, Width = Dim.Width(label) };

        var messagesToMove = (int)QueueListView.SelectedObject.Properties.DeadLetterMessageCount;
        input.TextChanging += args =>
        {
            if (int.TryParse(args.NewText.ToString(), out var count))
            {
                messagesToMove = count;
            }
            else
            {
                args.Cancel = true;
            }
        };

        move.Clicked += () =>
        {
            var result = MessageBox.Query($"Move {messagesToMove} messages", "Are you sure?", "Yes", "No");
            if (result == 1) return;

            var count = QueueListView.SelectedObject.Properties.Manager.MoveDeadLetters(QueueListView.SelectedObject.Properties.Name, messagesToMove, CancellationToken.None).GetAwaiter().GetResult();

            MessageBox.Query("Complete", $"Moved {count} messages", "Ok");
            QueueListView.RefreshQueue(QueueListView.SelectedObject);
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
        using var dialog = new Dialog("Message Details", ok);
        var messageBody = new TextView { X = 0, Y = 0, Width = Dim.Fill(1), Height = Dim.Percent(50), WordWrap = true };
        var messageError = new TextView { X = 0, Y = Pos.Bottom(messageBody) + 1, Width = Dim.Fill(1), Height = Dim.Fill(1), WordWrap = true };

        var bodyText = (string)obj.Table.Rows[obj.Row][1];
        messageBody.Text = bodyText;
        dialog.Add(messageBody);

        var errorText = (string)obj.Table.Rows[obj.Row][2];
        messageError.Text = errorText;
        dialog.Add(messageError);

        Application.Run(dialog);
    }

    private DataTable CreateTable(QueueProperties q)
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
    private DataTable CreateTable(IEnumerable<QueueMessage> messages)
    {
        var table = new DataTable();
        table.Columns.Add("Time");
        table.Columns.Add("Data");
        table.Columns.Add("Error");

        foreach (var message in messages)
        {
            table.Rows.Add(message.Time, message.Body, message.Error);
        }

        return table;
    }
}
