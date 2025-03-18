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
                            configuration.Database = databaseId;
                            configuration.Container = containerId;
                            configuration.PollingDelay = TimeSpan.FromMilliseconds(500);
                            configuration.DefaultTimeToLive = TimeSpan.FromSeconds(120);
                            configuration.Topics = new List<string> {"Topic1", "Topic2"};
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
            
            var client =
                (CosmosBus)knightBusHost.Services.CreateScope().ServiceProvider.GetRequiredService<CosmosBus>();
            
            //Send messages
            for (int i = 1; i <= 10; i++)
            {
                string topic = $"Topic{i % 4}";
                await client.PublishAsync(new SampleCosmosEvent(i.ToString(), topic), CancellationToken.None);
            }
            
            //Clean-up
            client.cleanUp();
            Console.WriteLine("End of program, press any key to exit.");
            Console.ReadKey();
        }
    }
    
    class CosmosEventProcessor :
        IProcessEvent<SampleCosmosEvent, SampleSubscription, CosmosProcessingSetting>
    {

        public Task ProcessAsync(SampleCosmosEvent message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Event 1: '{message.MessageBody}'");
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
        public string Topic { get; }
        public string? MessageBody { get; }

        //Single topic constructor
        public SampleCosmosEvent(string id, string topic, string? data = null)
        {
            //Throw exception if either of args are null
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            //Assign values
            this.id = id;
            this.Topic = topic;
            this.MessageBody = data;
        }
    }
    
}
