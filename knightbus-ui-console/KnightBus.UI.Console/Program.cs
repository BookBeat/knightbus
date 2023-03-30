// See https://aka.ms/new-console-template for more information
using KnightBus.UI.Console;
using KnightBus.UI.Console.Providers;
using KnightBus.UI.Console.Providers.ServiceBus;
using KnightBus.UI.Console.Providers.StorageBus;
using Terminal.Gui;

var connection = "";
var storage = "";

Application.Init();
Application.Run(new MainWindow(new IQueueManager[] { new ServiceBusQueueManager(connection), new ServiceBusTopicManager(connection), new StorageQueueManager(storage) }));



// Before the application exits, reset Terminal.Gui for clean shutdown
Application.Shutdown();
