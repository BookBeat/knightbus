﻿using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.DistributedTracing;
using KnightBus.Core.Sagas;
using KnightBus.Host;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Examples.Azure.Storage;

internal class Program
{
    static async Task Main(string[] args)
    {
        var storageConnection = "UseDevelopmentStorage=true";

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
                    .UseBlobStorage(storageConnection)
                    .RegisterProcessors(typeof(SampleStorageBusMessage).Assembly)
                    //Allow message processors to run in Singleton state using Azure Blob Locks
                    .UseBlobStorageLockManager()
                    .UseBlobStorageAttachments()
                    .UseTransport<StorageTransport>()
                    //Enable Saga support using the table storage Saga store
                    .UseBlobStorageSagas()
                    .UseDistributedTracing();
            })
            .UseKnightBus()
            .Build();

        //Start the KnightBus Host, it will now connect to the StorageBus and listen to the SampleStorageBusMessageMapping.QueueName
        await knightBus.StartAsync(CancellationToken.None);

        using var scope = knightBus.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IStorageBus>();
        await Task.Delay(TimeSpan.FromSeconds(10));

        //Send some Messages and watch them print in the console
        for (var i = 0; i < 10; i++)
        {
            await client.SendAsync(
                new SampleStorageBusMessage
                {
                    Message = $"Hello from command {i}",
                    Attachment = new MessageAttachment(
                        $"file{i}.txt",
                        "text/plain",
                        new MemoryStream(
                            Encoding.UTF8.GetBytes($"this is a stream from Message {i}")
                        )
                    ),
                }
            );
        }

        await client.SendAsync(
            new SampleSagaStartMessage { Message = "This is a saga start message" }
        );
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

    class SampleStorageBusMessageProcessor
        : IProcessCommand<SampleStorageBusMessage, SomeProcessingSetting>
    {
        private readonly IDistributedTracingProvider _distributedTracingProvider;

        public SampleStorageBusMessageProcessor(
            IDistributedTracingProvider distributedTracingProvider
        )
        {
            _distributedTracingProvider = distributedTracingProvider;
        }

        public Task ProcessAsync(
            SampleStorageBusMessage message,
            CancellationToken cancellationToken
        )
        {
            using (var streamReader = new StreamReader(message.Attachment.Stream))
            {
                Console.WriteLine($"Received command: '{message.Message}'");
                Console.WriteLine($"Attach file contents:'{streamReader.ReadToEnd()}'");
                Console.WriteLine(
                    $"Trace id: {_distributedTracingProvider.GetProperties()[DistributedTracingUtility.TraceIdKey]}"
                );
            }

            return Task.CompletedTask;
        }
    }

    class SampleSagaMessageProcessor
        : Saga<MySagaData>,
            IProcessCommand<SampleSagaMessage, SomeProcessingSetting>,
            IProcessCommand<SampleSagaStartMessage, SomeProcessingSetting>
    {
        private readonly IStorageBus _storageBus;

        public SampleSagaMessageProcessor(IStorageBus storageBus)
        {
            _storageBus = storageBus;
            //Map the messages to the Saga
            MessageMapper.MapStartMessage<SampleSagaStartMessage>(m => m.Id);
            MessageMapper.MapMessage<SampleSagaMessage>(m => m.Id);
        }

        public override string PartitionKey => "sample-saga";
        public override TimeSpan TimeToLive => TimeSpan.FromHours(1);

        public async Task ProcessAsync(
            SampleSagaStartMessage message,
            CancellationToken cancellationToken
        )
        {
            Console.WriteLine(message.Message);
            await _storageBus.SendAsync(new SampleSagaMessage());
        }

        public async Task ProcessAsync(
            SampleSagaMessage message,
            CancellationToken cancellationToken
        )
        {
            Console.WriteLine($"Counter is {Data.Counter}");
            if (Data.Counter == 5)
            {
                Console.WriteLine("Finishing Saga");
                await CompleteAsync(CancellationToken.None);
                return;
            }

            Data.Counter++;
            await UpdateAsync(CancellationToken.None);
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
