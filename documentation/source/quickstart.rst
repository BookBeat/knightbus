Quick Start
===========

The goal with Knightbus was to build a fast and simple messaging library that supports having multiple active messaging transports at the same time. 
There are many messaging frameworks available, but most of them are very complex and only support one active message transport, forcing developers to choose a fit-all messaging stack.

Installation
------------

Find the official KnightBus packages at NuGet.org : https://www.nuget.org/profiles/BookBeat


Processing Messages
-------------------

.. code-block:: c#

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

    public class SampleCommand : IServiceBusCommand
    {
        public string Message { get; set; }
    }

    public class SampleCommandMapping : IMessageMapping<SampleCommand>
    {
        public string QueueName => "your-queue-name";
    }

    public class SampleSettings : IProcessingSettings
    {
        //These settings can be re-used by multiple message processors
        public int MaxConcurrentCalls => 100; //How many concurrent messages do you want to process
        public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1); //How long should you process before considering the message hung
        public int DeadLetterDeliveryLimit => 1; //How many retries before dead lettering
        public int PrefetchCount => 50; //Increase perfomance by fetching more messages at once
    }


Starting as a Hosted Service (Recommended)
------------------------------------------

Using a hosted service will allow graceful(ish) shutdown of running instance and message processors.

.. code-block:: c#

    class Program
    {
        static async Task Main(string[] args)
        {
                var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    //Multiple active transports
                    services.UseServiceBus(config => config.ConnectionString = "sb-connection")
                            .UseTransport<ServiceBusTransport>()
                            .UseBlobStorage(config => config.ConnectionString = "storage-connection")
                            .UseTransport<StorageTransport>()
                            .RegisterProcessors();
                })
                .UseKnightBus().Build();                
    
                await host.StartAsync(CancellationToken.None);
        }
    }


Sending Messages
----------------

.. code-block:: c#

    var client = new ServiceBus(new ServiceBusConfiguration(serviceBusConnection));
    //Send some Commands
    for (var i = 0; i < 10; i++)
    {
        await client.SendAsync(new SampleCommand { Message = "Hello from message " + i.ToString() });
    }

Examples
--------

You can find all current examples at our GitHub repository https://github.com/BookBeat/knightbus/tree/master/knightbus/examples