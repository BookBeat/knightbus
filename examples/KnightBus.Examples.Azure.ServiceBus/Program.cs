using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.ServiceBus;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.SimpleInjector;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace KnightBus.Examples.Azure.ServiceBus
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var serviceBusConnection = "your-connection-string";

            //Register the container
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            container.Register<IProcessCommand<SampleServiceBusMessage, SomeProcessingSetting>, SampleServiceBusMessageProcessor>(Lifestyle.Scoped);
            //Or register all commands from an assembly
            //container.Register(typeof(IProcessCommand<,>), typeof(SampleServiceBusMessage).Assembly, Lifestyle.Scoped);
            container.Verify();

            //Initialize the KnightBus Host
            var knightBusHost = new KnightBusHost()
                //Enable the ServiceBus Transport
                .UseTransport(new ServiceBusTransport(serviceBusConnection))
                .Configure(configuration => configuration
                    //Use SimpleInjector to resolve our MessageProcessors
                    .UseSimpleInjector(container)
                );

            //Start the KnightBus Host, it will now connect to the ServiceBus and listen to the SampleServiceBusMessageMapping.QueueName
            await knightBusHost.StartAsync();

            //Initiate the client
            var client = new KnightBus.Azure.ServiceBus.ServiceBus(new ServiceBusConfiguration(serviceBusConnection));
            //Send some Messages and watch them print in the console
            for (var i = 0; i < 10; i++)
            {
                await client.SendAsync(new SampleServiceBusMessage { Message = $"Hello from message {i}" });
            }

            
            Console.ReadKey();
        }

        class SampleServiceBusMessage : IServiceBusCommand
        {
            public string Message { get; set; }
        }
        class SampleServiceBusMessageMapping : IMessageMapping<SampleServiceBusMessage>
        {
            public string QueueName => "your-queue-name";
        }

        class SampleServiceBusMessageProcessor : IProcessCommand<SampleServiceBusMessage, SomeProcessingSetting>
        {
            public Task ProcessAsync(SampleServiceBusMessage message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Message processor received: '{message.Message}'");
                return Task.CompletedTask;
            }
        }

        class SomeProcessingSetting : IProcessingSettings
        {
            public int MaxConcurrentCalls => 1;
            public int PrefetchCount => 1;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
            public int DeadLetterDeliveryLimit => 2;
        }
    }
}
