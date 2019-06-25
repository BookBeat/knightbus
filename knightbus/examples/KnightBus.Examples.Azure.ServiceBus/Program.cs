using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.ServiceBus;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;

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

            var knightBusHost = new KnightBusHost()
                //Enable the ServiceBus Transport
                .UseTransport(new ServiceBusTransport(serviceBusConnection))
                .Configure(configuration => configuration
                    //Register our message processors without IoC using the standard provider
                    .UseMessageProcessorProvider(new StandardMessageProcessorProvider()
                        .RegisterProcessor(new SampleServiceBusMessageProcessor())
                        .RegisterProcessor(new SampleServiceBusEventProcessor()))
                );

            //Start the KnightBus Host, it will now connect to the ServiceBus and listen to the SampleServiceBusMessageMapping.QueueName
            await knightBusHost.StartAsync();

            //Initiate the client
            var client = new KnightBus.Azure.ServiceBus.ServiceBus(new ServiceBusConfiguration(serviceBusConnection));
            //Send some Messages and watch them print in the console
            for (var i = 0; i < 10; i++)
            {
                await client.SendAsync(new SampleServiceBusMessage { Message = $"Hello from command {i}" });
            }
            for (var i = 0; i < 10; i++)
            {
                await client.PublishEventAsync(new SampleServiceBusEvent { Message = $"Hello from event {i}" });
            }


            Console.ReadKey();
        }

        class SampleServiceBusMessage : IServiceBusCommand
        {
            public string Message { get; set; }
        }

        class SampleServiceBusEvent : IServiceBusEvent
        {
            public string Message { get; set; }
        }

        class SampleServiceBusMessageMapping : IMessageMapping<SampleServiceBusMessage>
        {
            public string QueueName => "your-queue";
        }

        class SampleServiceBusEventMapping : IMessageMapping<SampleServiceBusEvent>
        {
            public string QueueName => "your-topic";
        }

        class SampleServiceBusMessageProcessor :
            IProcessCommand<SampleServiceBusMessage, SomeProcessingSetting>,
            IProcessEvent<SampleServiceBusEvent, EventSubscriptionOne, SomeProcessingSetting>
        {
            public Task ProcessAsync(SampleServiceBusMessage message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Received command: '{message.Message}'");
                return Task.CompletedTask;
            }

            public Task ProcessAsync(SampleServiceBusEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Received event: '{message.Message}'");
                return Task.CompletedTask;
            }
        }

        class SampleServiceBusEventProcessor : 
            IProcessEvent<SampleServiceBusEvent, EventSubscriptionTwo, SomeProcessingSetting>,
            IProcessDeadletter<SampleServiceBusEvent>
        {
            public Task ProcessAsync(SampleServiceBusEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Also received event: '{message.Message}'");
                throw new Exception("Trigger retry");
            }

            public Task BeforeDeadLetterAsync(SampleServiceBusEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Dead lettering event: '{message.Message}'");
                return Task.CompletedTask;
            }
        }

        class EventSubscriptionOne : IEventSubscription<SampleServiceBusEvent>
        {
            public string Name => "subscription-1";
        }
        class EventSubscriptionTwo : IEventSubscription<SampleServiceBusEvent>
        {
            public string Name => "subscription-2";
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
