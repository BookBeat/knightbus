// See https://aka.ms/new-console-template for more information

using KnightBus.UI.Console;
using KnightBus.UI.Console.Providers.StorageBus;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;

var config = Config.LoadConfig();

IServiceCollection collection = new ServiceCollection();

collection
    .UseServiceBus(config)
    .UseBlobStorage(config);


collection.AddSingleton<MainWindow>();

var provider = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });


Application.Init();
Application.Run(provider.GetRequiredService<MainWindow>(), exception => MessageBox.ErrorQuery("Error", $"{exception.Message}\n\n{exception.StackTrace}", "Ok") == 0);



// Before the application exits, reset Terminal.Gui for clean shutdown
Application.Shutdown();
