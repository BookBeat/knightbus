using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.Nats;
using KnightBus.Nats.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Examples.Nats
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var connectionString = "localhost";
            var storageConnection = "UseDevelopmentStorage=true";
            // Start nats.io first
            // $ docker run -p 4222:4222 -ti nats:latest

            var knightBus = global::Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
                })
                .ConfigureServices(services =>
                {
                    services
                        .UseBlobStorage(storageConnection)
                        .UseBlobStorageAttachments()
                        .UseBlobStorageSagas()
                        .UseNats(configuration => configuration.ConnectionString = connectionString)
                        .RegisterProcessors(typeof(NatsBusCommandProcessor).Assembly)
                        //Enable the Nats Transport
                        .UseTransport<NatsTransport>();

                })
                .UseKnightBus()
                .Build();
            //Start the KnightBus Host, it will now connect to the StorageBus and listen to the SampleStorageBusMessageMapping.QueueName
            await knightBus.StartAsync(CancellationToken.None);

            var client = (NatsBus)knightBus.Services.CreateScope().ServiceProvider.GetRequiredService<INatsBus>();

            //Send some Messages and watch them print in the console
            for (var i = 0; i < 1; i++)
            {
                var response =
                    client.RequestStream<SampleNatsMessage, SampleNatsReply>(new SampleNatsMessage { Message = $"Hello from command {i}" });
                foreach (var reply in response)
                {
                    Console.WriteLine(reply.Reply);
                }
            }

            await client.Publish(new SampleNatsEvent(), CancellationToken.None);
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello"));
            stream.Position = 0;
            await client.Send(new SampleNatsCommand { Attachment = new MessageAttachment("file.txt", "txt", stream) });
            Console.ReadKey();
        }

        class SampleNatsMessage : INatsRequest
        {
            public string Message { get; set; }
        }

        class SampleNatsCommand : INatsCommand, ICommandWithAttachment
        {
            public IMessageAttachment Attachment { get; set; }
        }

        class SampleNatsCommandMapping : IMessageMapping<SampleNatsCommand>
        {
            public string QueueName { get; } = "your-command";
        }

        class SampleNatsEvent : INatsEvent
        {
            public string Message { get; set; }
        }

        class SampleSubscription1 : IEventSubscription<SampleNatsEvent>
        {
            public string Name => "one";
        }

        class SampleSubscription2 : IEventSubscription<SampleNatsEvent>
        {
            public string Name => "two";
        }

        class SampleNatsReply
        {
            public string Reply { get; set; }
        }

        class SampleNatsMessageMapping : IMessageMapping<SampleNatsMessage>
        {
            public string QueueName => "your-queue";
        }

        class SampleNatsEventMapping : IMessageMapping<SampleNatsEvent>
        {
            public string QueueName => "your-queue-2";
        }


        class NatsBusStreamRequestProcessor : IProcessStreamRequest<SampleNatsMessage, SampleNatsReply, SomeProcessingSetting>
        {
            public async IAsyncEnumerable<SampleNatsReply> ProcessAsync(SampleNatsMessage message,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                for (int i = 0; i < 20; i++)
                {
                    yield return new SampleNatsReply { Reply = $"Async Reply {i}:\t {message.Message}" };
                }
            }
        }

        class NatsBusEventProcessor1 : IProcessEvent<SampleNatsEvent, SampleSubscription1, SomeProcessingSetting>
        {
            public Task ProcessAsync(SampleNatsEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Event listener 1");
                return Task.CompletedTask;
            }
        }

        class NatsBusEventProcessor2 : IProcessEvent<SampleNatsEvent, SampleSubscription2, SomeProcessingSetting>
        {
            public Task ProcessAsync(SampleNatsEvent message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Event listener 2");
                return Task.CompletedTask;
            }
        }

        class NatsBusCommandProcessor : IProcessCommand<SampleNatsCommand, SomeProcessingSetting>
        {
            public Task ProcessAsync(SampleNatsCommand message, CancellationToken cancellationToken)
            {
                using (var s = new StreamReader(message.Attachment.Stream))
                {
                    Console.WriteLine($"Command {s.ReadToEnd()}");
                }

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
