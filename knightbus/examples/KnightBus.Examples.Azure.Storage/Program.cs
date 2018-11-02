using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;

namespace KnightBus.Examples.Azure.Storage
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var storageConnection = "your-connection-string";

            var knightBusHost = new KnightBusHost()
                //Enable the StorageBus Transport
                .UseTransport(new StorageTransport(storageConnection)
                    //Enable attachments on the transport using Azure Blobs
                    .UseBlobStorageAttachments(storageConnection))
                .Configure(configuration => configuration
                    //Allow message processors to run in Singleton state using Azure Blob Locks
                    .UseBlobStorageLockManager(storageConnection)
                    //Register our message processors without IoC using the standard provider
                    .UseMessageProcessorProvider(new StandardMessageProcessorProvider()
                        .RegisterProcessor(new SampleStorageBusMessageProcessor()))
                );

            //Start the KnightBus Host, it will now connect to the StorageBus and listen to the SampleStorageBusMessageMapping.QueueName
            await knightBusHost.StartAsync();

            //Initiate the client
            var client = new StorageBus(new StorageBusConfiguration(storageConnection)
            {
                //The client must also have attachments enabled to send them
                AttachmentProvider = new BlobStorageMessageAttachmentProvider(storageConnection)
            });
            //Send some Messages and watch them print in the console
            for (var i = 0; i < 10; i++)
            {
                await client.SendAsync(new SampleStorageBusMessage
                {
                    Message = $"Hello from command {i}",
                    Attachment = new MessageAttachment($"file{i}.txt", "text/plain", new MemoryStream(Encoding.UTF8.GetBytes($"this is a stream from Message {i}")))
                });
            }

            Console.ReadKey();
        }

        class SampleStorageBusMessage : IStorageQueueCommand, ICommandWithAttachment
        {
            public string Message { get; set; }
            public IMessageAttachment Attachment { get; set; }
        }

        class SampleStorageBusMessageMapping : IMessageMapping<SampleStorageBusMessage>
        {
            public string QueueName => "your-queue";
        }

        class SampleStorageBusMessageProcessor : IProcessCommand<SampleStorageBusMessage, SomeProcessingSetting>
        {
            public Task ProcessAsync(SampleStorageBusMessage message, CancellationToken cancellationToken)
            {
                using (var streamReader = new StreamReader(message.Attachment.Stream))
                {
                    Console.WriteLine($"Received command: '{message.Message}'");
                    Console.WriteLine($"Attach file contents:'{streamReader.ReadToEnd()}'");
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
