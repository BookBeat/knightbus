# KnightBus

[![Build status](https://ci.appveyor.com/api/projects/status/6kut5wktspt8nhf5/branch/master?svg=true)](https://ci.appveyor.com/project/NiklasArbin/knightbus/branch/master) 
[![NuGet](https://img.shields.io/nuget/v/KnightBus.Core.svg)](https://www.nuget.org/packages/KnightBus.Core/) 
[![Documentation Status](https://readthedocs.org/projects/knightbus/badge/?version=latest)](https://knightbus.readthedocs.io/en/latest/?badge=latest)

## KnightBus is a fast, lightweight and extensible messaging framework that supports multiple active message transports


[Find the official KnightBus documentation here](https://knightbus.readthedocs.io/)

<img src="https://raw.githubusercontent.com/BookBeat/knightbus-documentation/master/media/images/knightbus-logo.png" alt="KnightBus Logo" width="300"/>


| Package | NuGet Stable | 
| ------- | ------------ |
| [KnightBus.Host](https://www.nuget.org/packages/KnightBus.Host/) | [![KnightBus.Host](https://img.shields.io/nuget/v/KnightBus.Host.svg)](https://www.nuget.org/packages/KnightBus.Host/) |
| [KnightBus.Core](https://www.nuget.org/packages/KnightBus.Core/) | [![KnightBus.Core](https://img.shields.io/nuget/v/KnightBus.Core.svg)](https://www.nuget.org/packages/KnightBus.Core/) |
| [KnightBus.Messages](https://www.nuget.org/packages/KnightBus.Messages/) | [![KnightBus.Messages](https://img.shields.io/nuget/v/KnightBus.Messages.svg)](https://www.nuget.org/packages/KnightBus.Messages/) |
| [KnightBus.Azure.ServiceBus](https://www.nuget.org/packages/KnightBus.Azure.ServiceBus/) | [![KnightBus.Azure.ServiceBus](https://img.shields.io/nuget/v/KnightBus.Azure.ServiceBus.svg)](https://www.nuget.org/packages/KnightBus.Azure.ServiceBus/) |
| [KnightBus.Azure.ServiceBus.Messages](https://www.nuget.org/packages/KnightBus.Azure.ServiceBus.Messages/) | [![KnightBus.Azure.ServiceBus.Messages](https://img.shields.io/nuget/v/KnightBus.Azure.ServiceBus.Messages.svg)](https://www.nuget.org/packages/KnightBus.Azure.ServiceBus.Messages/) |
| [KnightBus.Azure.Storage](https://www.nuget.org/packages/KnightBus.Azure.Storage/) | [![KnightBus.Azure.Storage](https://img.shields.io/nuget/v/KnightBus.Azure.Storage.svg)](https://www.nuget.org/packages/KnightBus.Azure.Storage/) |
| [KnightBus.Azure.Storage.Messages](https://www.nuget.org/packages/KnightBus.Azure.Storage.Messages/) | [![KnightBus.Azure.Storage.Messages](https://img.shields.io/nuget/v/KnightBus.Azure.Storage.Messages.svg)](https://www.nuget.org/packages/KnightBus.Azure.Storage.Messages/) |
| [KnightBus.Redis](https://www.nuget.org/packages/KnightBus.Redis/) | [![KnightBus.Redis](https://img.shields.io/nuget/v/KnightBus.Redis.svg)](https://www.nuget.org/packages/KnightBus.Redis/) |
| [KnightBus.Redis.Messages](https://www.nuget.org/packages/KnightBus.Redis.Messages/) | [![KnightBus.Redis.Messages](https://img.shields.io/nuget/v/KnightBus.Redis.Messages.svg)](https://www.nuget.org/packages/KnightBus.Redis.Messages/) |
| [KnightBus.Serilog](https://www.nuget.org/packages/KnightBus.Serilog/) | [![KnightBus.Serilog](https://img.shields.io/nuget/v/KnightBus.Serilog.svg)](https://www.nuget.org/packages/KnightBus.Serilog/) |
| [KnightBus.SimpleInjector](https://www.nuget.org/packages/KnightBus.SimpleInjector/) | [![KnightBus.SimpleInjector](https://img.shields.io/nuget/v/KnightBus.SimpleInjector.svg)](https://www.nuget.org/packages/KnightBus.SimpleInjector/) |
| [KnightBus.Microsoft.DependencyInjection](https://www.nuget.org/packages/KnightBus.Microsoft.DependencyInjection/) | [![KnightBus.Microsoft.DependencyInjection](https://img.shields.io/nuget/v/KnightBus.Microsoft.DependencyInjection.svg)](https://www.nuget.org/packages/KnightBus.Microsoft.DependencyInjection/) |
| [KnightBus.ApplicationInsights](https://www.nuget.org/packages/KnightBus.ApplicationInsights/) | [![KnightBus.ApplicationInsights](https://img.shields.io/nuget/v/KnightBus.ApplicationInsights.svg)](https://www.nuget.org/packages/KnightBus.ApplicationInsights/) |
| [KnightBus.SqlServer](https://www.nuget.org/packages/KnightBus.SqlServer/) | [![KnightBus.SqlServer](https://img.shields.io/nuget/v/KnightBus.SqlServer.svg)](https://www.nuget.org/packages/KnightBus.SqlServer/) |

## Message Processing
```csharp
public class CommandProcessor : IProcessCommand<SampleCommand, SampleSettings>,
{
    public CommandProcessor(ISomeDependency dependency)
    {
        //You can use your own container for dependency injection
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
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
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


