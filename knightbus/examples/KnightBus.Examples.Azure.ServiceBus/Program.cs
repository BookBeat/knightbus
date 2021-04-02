using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.ServiceBus;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.ProtoBufNet;
using ProtoBuf;

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

            var configuration = new ServiceBusConfiguration(serviceBusConnection)
                {MessageSerializer = new MicrosoftJsonSerializer()};
            
            var knightBusHost = new KnightBusHost()
                //Enable the ServiceBus Transport
                .UseTransport(new ServiceBusTransport(configuration))
                .Configure(configuration => configuration
                    //Register our message processors without IoC using the standard provider
                    .UseDependencyInjection(new StandardDependecyInjection()
                        .RegisterProcessor(new SampleServiceBusMessageProcessor())
                        .RegisterProcessor(new SampleServiceBusEventProcessor()))
                );

            //Start the KnightBus Host, it will now connect to the ServiceBus and listen to the SampleServiceBusMessageMapping.QueueName
            await knightBusHost.StartAsync(new CancellationToken());

            //Initiate the client
            var protoClient = new KnightBus.Azure.ServiceBus.ServiceBus(new ServiceBusConfiguration(serviceBusConnection){MessageSerializer = new ProtobufNetSerializer()});
            var jsonClient = new KnightBus.Azure.ServiceBus.ServiceBus(new ServiceBusConfiguration(serviceBusConnection){MessageSerializer = new MicrosoftJsonSerializer()});
            //Send some Messages and watch them print in the console
            for (var i = 0; i < 10; i++)
            {
                await protoClient.SendAsync(new SampleServiceBusMessage { Message = $"Hello from command {i}" });
            }
            for (var i = 0; i < 10; i++)
            {
                await jsonClient.PublishEventAsync(new SampleServiceBusEvent { Message = $"Hello from event {i}" });
            }


            Console.ReadKey();
        }

        [ProtoContract]
        class SampleServiceBusMessage : IServiceBusCommand
        {
            [ProtoMember(1)]
            public string Message { get; set; }
        }
        
        class SampleServiceBusEvent : IServiceBusEvent
        {
            public string Message { get; set; }
        }

        class SampleServiceBusMessageMapping : IMessageMapping<SampleServiceBusMessage>, IServiceBusCreationOptions
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

        class SampleServiceBusMessageProcessor :
            IProcessCommand<SampleServiceBusMessage, ProtoBufProcessingSetting>,
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
            IProcessBeforeDeadLetter<SampleServiceBusEvent>
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
        
        class ProtoBufProcessingSetting : IProcessingSettings, ICustomMessageSerializer
        {
            public int MaxConcurrentCalls => 1;
            public int PrefetchCount => 1;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
            public int DeadLetterDeliveryLimit => 2;
            public IMessageSerializer MessageSerializer => new ProtobufNetSerializer();
        }
    }
}
