using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Azure.Storage.Sagas;
using KnightBus.Core;
using KnightBus.Core.Sagas;
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

            //Initiate the client
            var client = new StorageBus(new StorageBusConfiguration(storageConnection));
            client.EnableAttachments(new BlobStorageMessageAttachmentProvider(storageConnection));

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
                        .RegisterProcessor(new SampleStorageBusMessageProcessor())
                        .RegisterProcessor(new SampleSagaMessageProcessor(client))
                    )
                    //Enable Saga support using the table storage Saga store
                    .EnableSagas(new StorageTableSagaStore(storageConnection))
                );

            //Start the KnightBus Host, it will now connect to the StorageBus and listen to the SampleStorageBusMessageMapping.QueueName
            await knightBusHost.StartAsync();

            
            //Send some Messages and watch them print in the console
            //for (var i = 0; i < 10; i++)
            //{
            //    await client.SendAsync(new SampleStorageBusMessage
            //    {
            //        Message = $"Hello from command {i}",
            //        Attachment = new MessageAttachment($"file{i}.txt", "text/plain", new MemoryStream(Encoding.UTF8.GetBytes($"this is a stream from Message {i}")))
            //    });
            //}

            await client.SendAsync(new SampleSagaStartMessage { Message = "This is a saga start message" });
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

        class SampleSagaStartMessage : IStorageQueueCommand
        {
            public string Id { get; set; } = "c1e06984-d946-4c70-a8aa-e32e44c6407e";
            public string Message { get; set; }
        }
        class SampleSagaStartMessageMapping : IMessageMapping<SampleSagaStartMessage>
        {
            public string QueueName => "your-saga-start";
        }

        public class SampleSagaMessage : IStorageQueueCommand
        {
            public string Id { get; set; } = "c1e06984-d946-4c70-a8aa-e32e44c6407e";
        }
        class SampleSagaMessageMapping : IMessageMapping<SampleSagaMessage>
        {
            public string QueueName => "your-saga-message";
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

        class SampleSagaMessageProcessor: Saga<MySagaData>,
            IProcessCommand<SampleSagaMessage, SomeProcessingSetting>, 
            IProcessCommand<SampleSagaStartMessage, SomeProcessingSetting>
        {
            private readonly IStorageBus _storageBus;

            public SampleSagaMessageProcessor(IStorageBus storageBus)
            {
                _storageBus = storageBus;
                //Map the messages to the Saga
                MessageMapper.MapStartMessage<SampleSagaStartMessage>(m=> m.Id);
                MessageMapper.MapMessage<SampleSagaMessage>(m=> m.Id);
            }
            public override string PartitionKey => "sample-saga";
            public async Task ProcessAsync(SampleSagaStartMessage message, CancellationToken cancellationToken)
            {
                Console.WriteLine(message.Message);
                await _storageBus.SendAsync(new SampleSagaMessage());
            }

            public async Task ProcessAsync(SampleSagaMessage message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Counter is {Data.Counter}");
                if (Data.Counter == 10)
                {
                    Console.WriteLine("Finishing Saga");
                    await CompleteAsync();
                    return;
                }

                Data.Counter++;
                await UpdateAsync();
                await _storageBus.SendAsync(new SampleSagaMessage());
            }
        }

        class MySagaData
        {
            public int Counter { get; set; }
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
