using KnightBus.Core.DependencyInjection;
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
            const string deadLetterContainer = "deadLetterQueue";

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
                            configuration.deadLetterContainer = deadLetterContainer;
                            configuration.PollingDelay = TimeSpan.FromMilliseconds(500);
                            configuration.DefaultTimeToLive = TimeSpan.FromSeconds(120);
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
            
            var client = knightBusHost.Services.CreateScope().ServiceProvider.GetRequiredService<CosmosBus>();
            
            //Publish event
            for (int i = 1; i <= 5; i++)
            {
                await client.PublishAsync(new SampleCosmosEvent() { MessageBody = $"msg data {i}" }, CancellationToken.None);
            }
            
            //Publish other event
            for (int i = 1; i <= 2; i++)
            {
                await client.PublishAsync(new SampleCosmosEvent2() { data = $"data {i}" }, CancellationToken.None);
            }
            
            Console.ReadKey();

            //Publish poison event
            for (int i = 1; i <= 1; i++)
            {
                await client.PublishAsync(new SamplePoisonEvent() { bad_message = $"danger {i}" }, CancellationToken.None);
            }
            
            //Clean-up
            client.cleanUp();
            Console.WriteLine("End of program, press any key to exit.");
            Console.ReadKey();
        }
    }
    
    class CosmosEventProcessor :
        IProcessEvent<SampleCosmosEvent, SampleSubscription, CosmosProcessingSetting>,
        IProcessEvent<SampleCosmosEvent2, SampleSubscription2, CosmosProcessingSetting>,
        IProcessEvent<SampleCosmosEvent2, SampleSubscription3, CosmosProcessingSetting>,
        IProcessEvent<SamplePoisonEvent, SamplePoisonSubscription, CosmosProcessingSetting>
    {

        public Task ProcessAsync(SampleCosmosEvent message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Event 1: '{message.MessageBody}'");
            return Task.CompletedTask;
        }
        
        public Task ProcessAsync(SampleCosmosEvent2 message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Event 2: '{message.data}'");
            return Task.CompletedTask;
        }
        
        public Task ProcessAsync(SamplePoisonEvent message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Poison {message.bad_message}");
            throw new InvalidOperationException();
        }
    }
    
    class CosmosProcessingSetting : IProcessingSettings
    {
        public int MaxConcurrentCalls => 10; //Currently not used
        public int PrefetchCount => 50; //Currently not used
        public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5); //Currently not used
        public int DeadLetterDeliveryLimit => 2;
    }
    
    
    public class SampleCosmosEvent : ICosmosEvent
    {
        public string? MessageBody { get; set;  }
    }
    class SampleCosmosEventMapping : IMessageMapping<SampleCosmosEvent>
    {
        public string QueueName => "test-topic";
    }
    class SampleSubscription: IEventSubscription<SampleCosmosEvent>
    {
        public string Name => "sample_subscription";
    }

    
    public class SampleCosmosEvent2 : ICosmosEvent
    {
        public string? data { get; set; }
    }
    class SampleCosmosEventMapping2 : IMessageMapping<SampleCosmosEvent2>
    {
        public string QueueName => "test-topic2";
    }
    
    class SampleSubscription2: IEventSubscription<SampleCosmosEvent2>
    {
        public string Name => "sample_subscription2";
    }
    
    class SampleSubscription3: IEventSubscription<SampleCosmosEvent2>
    {
        public string Name => "sample_subscription3";
    }
    
    
    public class SamplePoisonEvent : ICosmosEvent
    {
        public string? bad_message { get; set;  }
    }
    class SamplePoisonEventMapping : IMessageMapping<SamplePoisonEvent>
    {
        public string QueueName => "poison-topic";
    }
    class SamplePoisonSubscription: IEventSubscription<SamplePoisonEvent>
    {
        public string Name => "sample_poison_subscription";
    }
    
}
