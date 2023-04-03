using System.Data;
using KnightBus.UI.Console.Providers;
using KnightBus.UI.Console.Tree.Nodes;
using NStack;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace KnightBus.UI.Console;

public sealed class QueueDetails : FrameView
{
    private QueueNode _selectedNode;

    public event Action<QueuePropertiesChangedEventArgs> QueuePropertiesChanged;

    public QueueDetails(ustring title, Border border = null) : base(title, border)
    { }

    public void QueueListViewOnSelectionChanged(object sender, SelectionChangedEventArgs<QueueNode> e)
    {
        RemoveAll();
        if (e.NewValue?.IsQueue != true) return;
        //Show queue details
        _selectedNode = e.NewValue;

        var queueTableView = new TableView(CreateTable(_selectedNode.Properties))
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(50),
        };

        Add(queueTableView);

        var peekMessagesButton = new Button("Peek Messages")
        {
            X = 1,
            Y = Pos.Bottom(queueTableView) + 1,
        };
        Add(peekMessagesButton);
        var messagesTableView = new TableView
        {
            Y = Pos.Bottom(peekMessagesButton),
            Width = Dim.Fill(),
            Height = Dim.Percent(50),
            Visible = false
        };
        messagesTableView.CellActivated += DeadLetterTableViewOnCellActivated;
        Add(messagesTableView);

        peekMessagesButton.Clicked += async delegate
        {
            var messages = await _selectedNode.Properties.Manager.Peek(_selectedNode.Properties.Name, 10, CancellationToken.None);
            messagesTableView.Table = CreateTable(messages);
            messagesTableView.Visible = true;
        };

        var peekDeadletterButton = new Button("Peek Deadletters")
        {
            X = Pos.Right(peekMessagesButton) + 1,
            Y = Pos.Bottom(queueTableView) + 1,
        };
        Add(peekDeadletterButton);
        peekDeadletterButton.Clicked += async delegate
        {
            var deadletters = await _selectedNode.Properties.Manager.PeekDeadLetter(_selectedNode.Properties.Name, 10, CancellationToken.None);
            messagesTableView.Table = CreateTable(deadletters);
            messagesTableView.Visible = true;
        };


        var moveButton = new Button("Move Deadletters")
        {
            X = Pos.Right(peekDeadletterButton) + 1,
            Y = Pos.Bottom(queueTableView) + 1,
        };
        moveButton.Clicked += MoveDeadletterMessages;
        Add(moveButton);
    }



    public void MoveDeadletterMessages()
    {
        if (_selectedNode?.IsQueue != true) return;

        var cancel = new Button("Cancel", true);
        var move = new Button("Move");
        cancel.Clicked += () => { Application.RequestStop(); };


        using var dialog = new Dialog(" Move Deadletter Messages", cancel, move);
        var queueLabel = new Label(_selectedNode.Properties.Name)
        {
            X = Pos.Center(),
            Y = 1
        };

        var label = new Label("Number of messages to Move")
        {
            X = 1,
            Y = 3
        };
        var input = new TextField(_selectedNode.Properties.DeadLetterMessageCount.ToString()) { X = label.X, Y = label.Y + 1, Width = Dim.Width(label) };

        var messagesToMove = (int)_selectedNode.Properties.DeadLetterMessageCount;
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

            var count = _selectedNode.Properties.Manager.MoveDeadLetters(_selectedNode.Properties.Name, messagesToMove, CancellationToken.None).GetAwaiter().GetResult();

            MessageBox.Query("Complete", $"Moved {count} messages", "Ok");
            QueuePropertiesChanged?.Invoke(new QueuePropertiesChangedEventArgs(_selectedNode));
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
        table.Columns.Add(q.Manager.DisplayName);
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

public class QueuePropertiesChangedEventArgs : EventArgs
{
    public QueueNode Node { get; }

    public QueuePropertiesChangedEventArgs(QueueNode node)
    {
        Node = node;
    }
}
