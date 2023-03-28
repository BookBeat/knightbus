// See https://aka.ms/new-console-template for more information
using KnightBus.UI.Console;
using Terminal.Gui;

var connection = "";

Application.Init();
Application.Run(new MainWindow(new ServiceBusQueueManager(connection)));



// Before the application exits, reset Terminal.Gui for clean shutdown
Application.Shutdown();
