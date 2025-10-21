using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.ServiceBus;
using KnightBus.Azure.ServiceBus.Management;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.PreProcessors;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Examples.Azure.ServiceBus;

class Program
{
    static async Task Main(string[] args)
    {
        var serviceBusConnection = "your-connection-string";

        var knightBus = Microsoft
            .Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices(services =>
            {
                services
                    // Connect with connection string
                    .UseServiceBus(config => config.ConnectionString = serviceBusConnection)
                    // Or connect with managed identity, azure entra id etc
                    // .UseServiceBus(config =>
                    // {
                    //     config.FullyQualifiedNamespace = "example.servicebus.windows.net";
                    //     config.Credential = new ManagedIdentityCredential();
                    // })
                    .RegisterProcessors(typeof(SampleServiceBusEventProcessor).Assembly)
                    .UseTransport<ServiceBusTransport>();
            })
            .UseKnightBus()
            .Build();

        //Start the KnightBus Host, it will now connect to the ServiceBus and listen to the SampleServiceBusMessageMapping.QueueName
        await knightBus.StartAsync(CancellationToken.None);

        //Initiate the client
        var client = new KnightBus.Azure.ServiceBus.ServiceBus(
            new ServiceBusConfiguration(serviceBusConnection)
            {
                MessageSerializer = new NewtonsoftSerializer(),
            },
            new ClientFactory(new ServiceBusConfiguration(serviceBusConnection)),
            Enumerable.Empty<IMessagePreProcessor>()
        );
        var jsonClient = new KnightBus.Azure.ServiceBus.ServiceBus(
            new ServiceBusConfiguration(serviceBusConnection),
            new ClientFactory(new ServiceBusConfiguration(serviceBusConnection)),
            Enumerable.Empty<IMessagePreProcessor>()
        );
        var managementClient = new ServiceBusQueueManager(
            new ServiceBusConfiguration(serviceBusConnection)
        );

        //Send some Messages and watch them print in the console
        for (var i = 0; i < 10; i++)
        {
            await client.SendAsync(
                new SampleServiceBusMessage { Message = $"Hello from command {i}" }
            );
        }
        for (var i = 0; i < 10; i++)
        {
            await jsonClient.PublishEventAsync(
                new SampleServiceBusEvent { Message = $"Hello from event {i}" }
            );
        }

        // Send a message with management client
        await managementClient.SendMessage(
            "other-queue",
            "{\"SomeProperty\": \"hello, world!\"}",
            default
        );

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

    class OtherSampleServiceBusMessage : IServiceBusCommand
    {
        public string SomeProperty { get; set; }
    }

    class OtherSampleServiceBusMessageMapping : IMessageMapping<OtherSampleServiceBusMessage>
    {
        public string QueueName => "other-queue";
    }

    class SampleServiceBusMessageMapping
        : IMessageMapping<SampleServiceBusMessage>,
            IServiceBusCreationOptions
    {
        public string QueueName => "your-queue";
        public bool EnablePartitioning => true;
        public bool SupportOrdering => false;
        public bool EnableBatchedOperations => true;
    }

    class SampleServiceBusEventMapping : IMessageMapping<SampleServiceBusEvent>
    {
        public string QueueName => "your-topic";
    }

    class SampleServiceBusMessageProcessor
        : IProcessCommand<SampleServiceBusMessage, SomeProcessingSetting>,
            IProcessCommand<OtherSampleServiceBusMessage, SomeProcessingSetting>,
            IProcessEvent<SampleServiceBusEvent, EventSubscriptionOne, SomeProcessingSetting>
    {
        public Task ProcessAsync(
            SampleServiceBusMessage message,
            CancellationToken cancellationToken
        )
        {
            Console.WriteLine($"Received command: '{message.Message}'");
            return Task.CompletedTask;
        }

        public Task ProcessAsync(SampleServiceBusEvent message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Received event: '{message.Message}'");
            return Task.CompletedTask;
        }

        public Task ProcessAsync(
            OtherSampleServiceBusMessage message,
            CancellationToken cancellationToken
        )
        {
            Console.WriteLine($"Received command: '{message.SomeProperty}'");
            return Task.CompletedTask;
        }
    }

    class SampleServiceBusEventProcessor
        : IProcessEvent<SampleServiceBusEvent, EventSubscriptionTwo, SomeProcessingSetting>,
            IProcessBeforeDeadLetter<SampleServiceBusEvent>
    {
        public Task ProcessAsync(SampleServiceBusEvent message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Also received event: '{message.Message}'");
            throw new Exception("Trigger retry");
        }

        public Task BeforeDeadLetterAsync(
            SampleServiceBusEvent message,
            CancellationToken cancellationToken
        )
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
