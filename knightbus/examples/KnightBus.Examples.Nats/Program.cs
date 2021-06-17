using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.Nats;

namespace KnightBus.Examples.Nats
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var connectionString = "localhost";

            // Start nats.io first
            // $ docker run -p 4222:4222 -ti nats:latest
            
            //Initiate the client
             var client = new NatsBus(new NatsBusConfiguration(connectionString));

            var knightBusHost = new KnightBusHost()
                //Enable the Nats Transport
                .UseTransport(new NatsTransport(connectionString))
                .Configure(configuration => configuration
                    //Register our message processors without IoC using the standard provider
                    .UseDependencyInjection(new StandardDependecyInjection()
                        .RegisterProcessor(new SampleNatsBusMessageProcessor())
                    )
                );

            //Start the KnightBus Host, it will now connect to the StorageBus and listen to the SampleStorageBusMessageMapping.QueueName
            await knightBusHost.StartAsync(CancellationToken.None);

            await client.SendAsync(new SampleNatsMessage
            {
                Message = "Hej Rabbit"
            });
            
            //Send some Messages and watch them print in the console
            for (var i = 0; i < 10; i++)
            {
                await client.SendAsync(new SampleNatsMessage
                {
                    Message = $"Hello from command {i}"
                });
            }
            Console.ReadKey();
        }

        class SampleNatsMessage : INatsCommand
        {
            public string Message { get; set; }
        }

        class SampleNatsMessageMapping : IMessageMapping<SampleNatsMessage>
        {
            public string QueueName => "your-queue";
        }

       
        class SampleNatsBusMessageProcessor : IProcessCommand<SampleNatsMessage, SomeProcessingSetting>
        {
            public Task ProcessAsync(SampleNatsMessage message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Received command: '{message.Message}'");
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
