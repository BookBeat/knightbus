using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.Nats;
using KnightBus.Nats.Messages;
using NATS.Client;

namespace KnightBus.Examples.Nats
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var connectionString = "localhost";

            // Start nats.io first
            // $ docker run -p 4222:4222 -ti nats:latest
            
            //Initiate the client
            var config = new NatsBusConfiguration(connectionString);
            var factory = new ConnectionFactory();
            var client = new NatsBus(factory.CreateConnection(), config);

            var knightBusHost = new KnightBusHost()
                //Enable the Nats Transport
                .UseTransport(new NatsTransport(connectionString))
                .Configure(configuration => configuration
                    //Register our message processors without IoC using the standard provider
                    .UseDependencyInjection(new StandardDependecyInjection()
                        .RegisterProcessor(new NatsBusStreamRequestProcessor())
                    )
                );

            //Start the KnightBus Host, it will now connect to the StorageBus and listen to the SampleStorageBusMessageMapping.QueueName
            await knightBusHost.StartAsync(CancellationToken.None);

            //Send some Messages and watch them print in the console
            for (var i = 0; i < 1; i++)
            {
                var response =  client.RequestStream<SampleNatsMessage, SampleNatsReply>(new SampleNatsMessage { Message = $"Hello from command {i}" });
                foreach (var reply in response)
                {
                    Console.WriteLine(reply.Reply);
                }
            }
            Console.ReadKey();
        }

        class SampleNatsMessage : INatsRequest
        {
            public string Message { get; set; }
        }

        class SampleNatsReply
        {
            public string Reply { get; set; }
        }

        class SampleNatsMessageMapping : IMessageMapping<SampleNatsMessage>
        {
            public string QueueName => "your-queue";
        }

       
        class NatsBusStreamRequestProcessor : IProcessStreamRequest<SampleNatsMessage, SampleNatsReply, SomeProcessingSetting>
        {
            public async IAsyncEnumerable<SampleNatsReply> ProcessAsync(SampleNatsMessage message, [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(1000);
                    yield return new SampleNatsReply { Reply = $"Async Reply {i}:\t {message.Message}" };
                }
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
