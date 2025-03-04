using System;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using Azure;
using KnightBus.Core.DependencyInjection;
using Microsoft.Azure.Cosmos;
using KnightBus.Cosmos;
using KnightBus.Cosmos.Messages;
using Microsoft.Extensions.Hosting;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Examples.CosmosDB
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting CosmosDB example");

            //Connection string should be saved as environment variable named "CosmosString"
            string? connectionString = Environment.GetEnvironmentVariable("CosmosString"); 
            const string databaseId = "db";
            const string containerId = "items";

            var knightBusHost = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
                })
                .ConfigureServices(services =>
                {
                    services.UseCosmos(configuration =>
                        {
                            configuration.ConnectionString = connectionString;
                            //configuration.DatabaseId = databaseId;
                            //configuration.ContainerId = containerId;
                            configuration.PollingDelay = TimeSpan.FromMilliseconds(250);
                        })
                        .RegisterProcessors(typeof(Program).Assembly) //Can be any class name in this project
                        .UseTransport<CosmosTransport>();
                })
                .UseKnightBus()
            .Build();
            
            
            //Start the KnightBus Host
            await knightBusHost.StartAsync();
            await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine("Started host");
            
            
            //Old, should be removed
            Program p = new Program();
            var cosmosClient = new CosmosClient(connectionString, new CosmosClientOptions() { ApplicationName = "ChangeFeedHost" });
            
            var client =
                (CosmosBus)knightBusHost.Services.CreateScope().ServiceProvider.GetRequiredService<CosmosBus>();
            
            //Set up database and containers
            await p.CreateDatabaseAsync(cosmosClient, databaseId);
            Container queue = await p.CreateContainerAsync(cosmosClient, databaseId, containerId, "/topic");

            //Create Host with ChangeFeed - should be moved to host that is created using DI
            Container leaseContainer = await p.CreateContainerAsync(cosmosClient, databaseId, "lease", "/id");
            ChangeFeedProcessor changeFeedProcessor = queue
                .GetChangeFeedProcessorBuilder<SampleCosmosEvent>(
                    processorName: "changeFeedSample",
                    onChangesDelegate: HandleChangesAsync)
                .WithInstanceName("consoleHost")
                .WithLeaseContainer(leaseContainer)
                .WithPollInterval(System.TimeSpan.FromMilliseconds(500))
                .Build();

            await changeFeedProcessor.StartAsync();
            Console.WriteLine("Change Feed Processor started (from program.cs) \n");
            
            //Send messages
            for (int i = 0; i < 2; i++)
            {
                await client.PublishAsync(new SampleCosmosEvent(i.ToString(), "testTopic"), CancellationToken.None);
            }
            
            //Clean-up
            client.cleanUp();
            Console.WriteLine("End of program, press any key to exit.");
            Console.ReadKey();
            
        }

        //Create database if it does not exist
        private async Task CreateDatabaseAsync(CosmosClient cosmosClient, string databaseId)
        {
            // Create a new database
            var response = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            if (response.StatusCode == HttpStatusCode.Created)
            {
                Console.WriteLine("Created Database: {0}\n", databaseId);
            } else if (response.StatusCode == HttpStatusCode.OK)
            {
                Console.WriteLine("DataBase already exists");
            }
            else
            {
                Console.WriteLine("Error, status code {0}",response.StatusCode);
            }
        }

        //Create Container if it does not exist, old containers with incompatible settings to the new one crash server
        public async Task<Container> CreateContainerAsync(CosmosClient cosmosClient, string databaseId, string containerId, string partitionKey)
        {
            //Create a new container
            Database database = cosmosClient.GetDatabase(databaseId);
            
            var response = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(containerId, partitionKey) {DefaultTimeToLive = 60});
            if (response.StatusCode == HttpStatusCode.Created)
            {
                Console.WriteLine("Created container: {0}\n", containerId);
            } else if (response.StatusCode == HttpStatusCode.OK)
            {
                Console.WriteLine("Container {0} already exists", containerId);
            }
            else
            {
                Console.WriteLine("Error, status: {0}",response.StatusCode);
            }

            return response.Container;
        }
        
        //Change Feed Handler
        private static Task HandleChangesAsync(
            IReadOnlyCollection<SampleCosmosEvent> changes, 
            CancellationToken cancellationToken)
        {
            foreach (var change in changes)
            {
                // Print the message_data received
                Console.WriteLine("Message {0} received with data: {1}",change.id, change.data);
            }
            return Task.CompletedTask;
        }
        
    }
    
    
    class CosmosEventProcessor :
        IProcessEvent<SampleCosmosEvent, SampleSubscription, CosmosProcessingSetting>
    {

        public Task ProcessAsync(SampleCosmosEvent message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Event 1: '{message.data}'");
            return Task.CompletedTask;
        }
    }
    
    class SamplePostgresEventMapping : IMessageMapping<SampleCosmosEvent>
    {
        public string QueueName => "sample_topic";
    }
    
    class CosmosProcessingSetting : IProcessingSettings
    {
        public int MaxConcurrentCalls => 10;
        public int PrefetchCount => 50;
        public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
        public int DeadLetterDeliveryLimit => 2;
    }

    class SampleSubscription: IEventSubscription<SampleCosmosEvent>
    {
        public string Name => "sample_subscription";
    }
    
    
    
    public class SampleCosmosEvent : ICosmosEvent
    {
        public string id { get; }
        public string topic { get; }
        public string? data { get; }

        public SampleCosmosEvent(string id, string topic, string? data = null)
        {
            //Throw exception if either of args are null
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(topic);
            //Assign values
            this.id = id;
            this.topic = topic;
            this.data = data;
        }
    }
    
}
