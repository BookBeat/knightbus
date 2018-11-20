using System;
using System.Diagnostics;
using System.Linq;
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
                    .AddMiddleware(new PerformanceLogging())
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
            }).ToList();

            sw.Start();
            await client.SendAsync<SampleStorageBusMessage>(tasks);
            

            Console.WriteLine($"Elapsed {sw.Elapsed}");
            Console.ReadKey();
        }

        class SampleStorageBusMessage : IRedisCommand
        {
            public string Message { get; set; }
            public string Id { get; } = Guid.NewGuid().ToString("N");
        }

        class SampleStorageBusMessageMapping : IMessageMapping<SampleStorageBusMessage>
        {
            public string QueueName => "your-queue";
        }

        class SampleStorageBusMessageProcessor : IProcessCommand<SampleStorageBusMessage, SomeProcessingSetting>
        {
            public Task ProcessAsync(SampleStorageBusMessage message, CancellationToken cancellationToken)
            {
                //Console.WriteLine($"Received command: '{message.Message}'");
                return Task.CompletedTask;
            }
        }

        public class PerformanceLogging : IMessageProcessorMiddleware
        {
            private int _count;
            private readonly Stopwatch _stopwatch = new Stopwatch();

            public PerformanceLogging()
            {
               
            }
            public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
            {
                if (!_stopwatch.IsRunning)
                {
                    _stopwatch.Start();
                }
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
                if (++_count % 1000 == 0)
                {
                    Console.WriteLine($"Processed {_count} messages in {_stopwatch.Elapsed} {_count / _stopwatch.Elapsed.TotalSeconds} m/s");
                }
            }
        }

        class SomeProcessingSetting : IProcessingSettings
        {
            public int MaxConcurrentCalls => 100000;
            public int PrefetchCount => 400;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
            public int DeadLetterDeliveryLimit => 2;
        }
    }
}
