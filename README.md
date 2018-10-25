# KnightBus

[![Build status](https://ci.appveyor.com/api/projects/status/6kut5wktspt8nhf5/branch/master?svg=true)](https://ci.appveyor.com/project/NiklasArbin/knightbus/branch/master) 
[![NuGet](https://img.shields.io/nuget/v/KnightBus.Core.svg)](https://www.nuget.org/packages/KnightBus.Core/) 
[![Documentation Status](https://readthedocs.org/projects/knightbus/badge/?version=latest)](https://knightbus.readthedocs.io/en/latest/?badge=latest)

## KnightBus is a fast, lightweight and extensible messaging framework that supports multiple active messaging transports

[Find the official KnightBus documentation here](https://knightbus.readthedocs.io/)

![KnightBus Logo](https://raw.githubusercontent.com/BookBeat/knightbus-documentation/master/media/images/knightbus-logo.png "KnightBus Logo")

## Message Processing
```csharp
public class CommandProcessor : IProcessCommand<SampleCommand, SampleSettings>,
{
    public CommandProcessor(ISomeDependency dependency)
    {
        //You can use bring your own container for dependency injection
    }

    public Task ProcessAsync(SampleCommand message, CancellationToken cancellationToken)
    {
        //Your code goes here
        return Task.CompletedTask;
    }
}
```

## Initialization
```csharp
class Program
{
    static void Main(string[] args)
    {
        MainAsync().GetAwaiter().GetResult();
    }

    static async Task MainAsync()
    {
        var serviceBusConnection = "your-connection-string";

        var knightBusHost = new KnightBusHost()
            .UseTransport(new ServiceBusTransport(serviceBusConnection))
            .Configure(configuration => configuration
                .UseMessageProcessorProvider(new StandardMessageProcessorProvider()
                    .RegisterProcessor(new CommandProcessor()))
            );

        await knightBusHost.StartAndBlockAsync();
    }
}
```

