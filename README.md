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
        var knightBusHost = new KnightBusHost()
            //Multiple active transports
            .UseTransport(new ServiceBusTransport("sb-connection"))
            .UseTransport(new StorageBusTransport("storage-connection"))
            .Configure(configuration => configuration
                .UseMessageProcessorProvider(new StandardMessageProcessorProvider()
                    .RegisterProcessor(new CommandProcessor()))
            );

        await knightBusHost.StartAndBlockAsync();
    }
}
```

## Bring your own Middleware

KnightBus supports inserting your own middleware into the execution pipeline. 

```csharp
public class CustomThrottlingMiddleware : IMessageProcessorMiddleware
    {
        private readonly SemaphoreQueue _semaphoreQueue;
        public int CurrentCount => _semaphoreQueue.CurrentCount;

        public CustomThrottlingMiddleware(int maxConcurrent)
        {
            _semaphoreQueue = new SemaphoreQueue(maxConcurrent);
        }
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            try
            {
                await _semaphoreQueue.WaitAsync().ConfigureAwait(false);
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphoreQueue.Release();
            }
        }
    }
```


