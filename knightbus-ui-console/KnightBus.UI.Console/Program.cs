// See https://aka.ms/new-console-template for more information
using KnightBus.UI.Console;
using KnightBus.UI.Console.Providers;
using KnightBus.UI.Console.Providers.ServiceBus;
using Terminal.Gui;

var connection = "";

Application.Init();
Application.Run(new MainWindow(new IQueueManager[] { new QueueManager(connection), new TopicManager(connection) }));



// Before the application exits, reset Terminal.Gui for clean shutdown
Application.Shutdown();
