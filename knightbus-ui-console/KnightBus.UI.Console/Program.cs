// See https://aka.ms/new-console-template for more information

using KnightBus.Azure.ServiceBus;
using KnightBus.Azure.Storage;
using KnightBus.UI.Console;
using KnightBus.UI.Console.Providers;
using KnightBus.UI.Console.Providers.ServiceBus;
using KnightBus.UI.Console.Providers.StorageBus;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;

var connection = "";
var storage = "";

IServiceCollection collection = new ServiceCollection();
collection.UseServiceBus(configuration => configuration.ConnectionString = connection);
collection.UseBlobStorage(configuration => configuration.ConnectionString = storage);
collection.AddSingleton<IQueueManager, ServiceBusQueueManager>();
collection.AddSingleton<IQueueManager, ServiceBusTopicManager>();
collection.AddSingleton<IQueueManager, StorageQueueManager>();
collection.AddSingleton<MainWindow>();

var provider = collection.BuildServiceProvider(new ServiceProviderOptions() { ValidateOnBuild = true, ValidateScopes = true });



Application.Init();
Application.Run(provider.GetRequiredService<MainWindow>(), exception => MessageBox.ErrorQuery("Error", $"{exception.Message}\n\n{exception.StackTrace}", "Ok") == 0);



// Before the application exits, reset Terminal.Gui for clean shutdown
Application.Shutdown();
