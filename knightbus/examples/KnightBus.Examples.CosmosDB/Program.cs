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

namespace KnightBus.Examples.CosmosDB
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Starting CosmosDB example");

            //Connection string found in CosmosDB
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
                            configuration.PollingDelay = TimeSpan.FromMilliseconds(250);
                        })
                        .RegisterProcessors(typeof(Program).Assembly) //Can be any class name in this project
                        .UseTransport<CosmosTransport>();
                })
                .UseKnightBus()
                .Build();

            
            
            
            
            Program p = new Program();
            
            //Initialize cosmos Client
            var cosmosClient = new CosmosClient(connectionString,
                new CosmosClientOptions() { ApplicationName = "ChangeFeedHost" });

            CosmosBus c = new CosmosBus(connectionString);
            
            //Set up database and containers
            await p.CreateDatabaseAsync(cosmosClient, databaseId);
            Container queue = await p.CreateContainerAsync(cosmosClient, databaseId, containerId, "/Topic");

            //Create Host with ChangeFeed
            Container leaseContainer = await p.CreateContainerAsync(cosmosClient, databaseId, "lease", "/id");
            ChangeFeedProcessor changeFeedProcessor = queue
                .GetChangeFeedProcessorBuilder<Message>(
                    processorName: "changeFeedSample",
                    onChangesDelegate: HandleChangesAsync)
                .WithInstanceName("consoleHost")
                .WithLeaseContainer(leaseContainer)
                .WithPollInterval(System.TimeSpan.FromMilliseconds(500))
                .Build();

            await changeFeedProcessor.StartAsync();
            Console.WriteLine("Change Feed Processor started \n");
            
            //Send messages
            for (int i = 0; i < 2; i++)
            {
                await c.SendAsync(new SampleCosmosCommand(), CancellationToken.None);
            }
            
            //Clean-up
            c.cleanUp();
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

        //Create Container if it does not exist
        public async Task<Container> CreateContainerAsync(CosmosClient cosmosClient, string databaseId, string containerId, string partitionKey)
        {
            //Create a new container
            Database database = cosmosClient.GetDatabase(databaseId);
            
            var response = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(containerId, partitionKey) {DefaultTimeToLive = 30});
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
            IReadOnlyCollection<Message> changes, 
            CancellationToken cancellationToken)
        {
            foreach (var change in changes)
            {
                // Print the message_data received
                Console.WriteLine("Message Data received: {0}",change.Value);
            }
            return Task.CompletedTask;
        }
        
    }

    class Messenger
    {
        public CosmosClient cosmosClient { get; set; }

        public async Task SendAsync(int id, string databaseId, string containerId)
        {
            // Create a Message object
            Message message = new Message
            {
                id = id.ToString(),
                Topic = "Topic",
                Value = "Hello from message " + id
            };

            try
            {
                // Read the item to see if it exists.  
                Container container = this.cosmosClient.GetContainer(databaseId, containerId);
                ItemResponse<Message> messageResponse = await container.ReadItemAsync<Message>(message.id, new PartitionKey(message.Topic));
                Console.WriteLine("Item in database with id: {0} already exists\n", messageResponse.Resource.id);
            }
            catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Create an item in the container on topic
                Container container = this.cosmosClient.GetContainer(databaseId, containerId);
                ItemResponse<Message> messageResponse = await container.CreateItemAsync<Message>(message, new PartitionKey(message.Topic));

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", messageResponse.Resource.id, messageResponse.RequestCharge);
            }
        }
    }

    public class SampleCosmosCommand : ICosmosCommand
    {
        public string message_id => "123";
    }
    
}
