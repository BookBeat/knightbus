// See https://aka.ms/new-console-template for more information

using Azure.Messaging.ServiceBus;
using KnightBus.UI.Console;
using Terminal.Gui;

Application.Run<MainWindow>();



// Before the application exits, reset Terminal.Gui for clean shutdown
Application.Shutdown();
