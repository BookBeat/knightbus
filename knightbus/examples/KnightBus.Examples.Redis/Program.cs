using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.Redis;
using KnightBus.Redis.Messages;

namespace KnightBus.Examples.Redis
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var redisConnection = "string";

            var knightBusHost = new KnightBusHost()
                //Enable the StorageBus Transport
                .UseTransport(new RedisTransport(redisConnection))
                .Configure(configuration => configuration
                    //Register our message processors without IoC using the standard provider
                    .UseMessageProcessorProvider(new StandardMessageProcessorProvider()
                        .RegisterProcessor(new SampleStorageBusMessageProcessor()))
                );

            //Start the KnightBus Host, it will now connect to the StorageBus and listen to the SampleStorageBusMessageMapping.QueueName
            await knightBusHost.StartAsync();

            //Initiate the client
            var client = new RedisBus(new RedisConfiguration(redisConnection)
            {
            });
            //Send some Messages and watch them print in the console
            var sw = new Stopwatch();
            
            var tasks = Enumerable.Range(0, 100000).Select(i => new SampleStorageBusMessage
            {
                Message = $"Hello from command {i}",
            });
            sw.Start();
            await client.SendAsync(tasks);

            Console.WriteLine($"Elapsed {sw.Elapsed}");
            Console.ReadKey();
        }

        class SampleStorageBusMessage : IRedisCommand
        {
            public string Message { get; set; }
        }

        class SampleStorageBusMessageMapping : IMessageMapping<SampleStorageBusMessage>
        {
            public string QueueName => "your-queue";
        }

        class SampleStorageBusMessageProcessor : IProcessCommand<SampleStorageBusMessage, SomeProcessingSetting>
        {
            public Task ProcessAsync(SampleStorageBusMessage message, CancellationToken cancellationToken)
            {
                Console.WriteLine($"Received command: '{message.Message}'");
                return Task.CompletedTask;
            }
        }

        class SomeProcessingSetting : IProcessingSettings
        {
            public int MaxConcurrentCalls => 1000;
            public int PrefetchCount => 1000;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
            public int DeadLetterDeliveryLimit => 2;
        }
    }
}
