using KnightBus.UI.Console.Providers;
using KnightBus.UI.Console.Tree;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;

namespace KnightBus.UI.Console;

public sealed class MainWindow : Window
{
    private readonly FrameView LeftPane;
    private readonly QueueTreeView QueueListView;
    private readonly QueueDetails RightPane;

    public MainWindow(IServiceProvider provider)
    {
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

        RightPane = new QueueDetails("Details")
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
        RightPane.QueuePropertiesChanged += QueueListView.OnQueuePropertiesChanged;

        Add(RightPane);
        QueueListView.LoadQueues();
        LeftPane.Add(QueueListView);
        QueueListView.SelectionChanged += RightPane.QueueListViewOnSelectionChanged;

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
                    new MenuItem("_Move Deadletters", "Move Deadletter Messages", RightPane.MoveDeadletterMessages, () => QueueListView.SelectedObject?.IsQueue == true, null, Key.M | Key.CtrlMask),
                    new MenuItem("_Delete", "Delete Queue", () => { QueueListView.DeleteQueue(QueueListView.SelectedObject); }, () => QueueListView.SelectedObject?.IsQueue == true)
                }),
        });
        Add(MenuBar);
    }
}
