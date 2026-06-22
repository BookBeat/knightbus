using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using KnightBus.LavinMQ;
using KnightBus.LavinMQ.Messages;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Examples.LavinMQ;

class Program
{
    // Start LavinMQ first:
    // $ docker run -p 5672:5672 -p 15672:15672 cloudamqp/lavinmq:latest
    private const string ConnectionString = "amqp://guest:guest@localhost:5672";

    static async Task Main(string[] args)
    {
        var knightBus = global::Microsoft
            .Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices(services =>
            {
                services
                    .UseLavinMQ(configuration => configuration.ConnectionString = ConnectionString)
                    .RegisterProcessors(typeof(SampleLavinMQCommandProcessor).Assembly)
                    // Enable the LavinMQ transport
                    .UseTransport<LavinMQTransport>();
            })
            .UseKnightBus()
            .Build();

        await knightBus.StartAsync(CancellationToken.None);

        var client = knightBus
            .Services.CreateScope()
            .ServiceProvider.GetRequiredService<ILavinMQBus>();

        // Command -> single processor
        await client.SendAsync(new SampleLavinMQCommand { Message = "Hello command" });

        // Event -> fanned out to both subscriptions
        await client.PublishAsync(new SampleLavinMQEvent { Message = "Hello event" });

        // Scheduled command -> delivered after the delay using LavinMQ's delayed-message exchange
        await client.ScheduleAsync(
            new SampleLavinMQCommand { Message = "Hello from the future" },
            TimeSpan.FromSeconds(5)
        );

        // This command always throws and will be dead-lettered to "lavinmq-example-failing-command.dl"
        await client.SendAsync(new FailingLavinMQCommand { Message = "I will fail" });

        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
    }

    class SampleLavinMQCommand : ILavinMQCommand
    {
        public string Message { get; set; } = string.Empty;
    }

    class SampleLavinMQCommandMapping : IMessageMapping<SampleLavinMQCommand>
    {
        public string QueueName => "lavinmq-example-command";
    }

    class FailingLavinMQCommand : ILavinMQCommand
    {
        public string Message { get; set; } = string.Empty;
    }

    class FailingLavinMQCommandMapping : IMessageMapping<FailingLavinMQCommand>
    {
        public string QueueName => "lavinmq-example-failing-command";
    }

    class SampleLavinMQEvent : ILavinMQEvent
    {
        public string Message { get; set; } = string.Empty;
    }

    class SampleLavinMQEventMapping : IMessageMapping<SampleLavinMQEvent>
    {
        public string QueueName => "lavinmq-example-event";
    }

    class SubscriptionOne : IEventSubscription<SampleLavinMQEvent>
    {
        public string Name => "one";
    }

    class SubscriptionTwo : IEventSubscription<SampleLavinMQEvent>
    {
        public string Name => "two";
    }

    class SampleLavinMQCommandProcessor
        : IProcessCommand<SampleLavinMQCommand, ExampleSettings>,
            IProcessEvent<SampleLavinMQEvent, SubscriptionOne, ExampleSettings>,
            IProcessEvent<SampleLavinMQEvent, SubscriptionTwo, ExampleSettings>
    {
        public Task ProcessAsync(SampleLavinMQCommand message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Command received: {message.Message}");
            return Task.CompletedTask;
        }

        public Task ProcessAsync(SampleLavinMQEvent message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Event received: {message.Message}");
            return Task.CompletedTask;
        }
    }

    class FailingLavinMQCommandProcessor : IProcessCommand<FailingLavinMQCommand, ExampleSettings>
    {
        public Task ProcessAsync(FailingLavinMQCommand message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Failing command received, will throw: {message.Message}");
            throw new InvalidOperationException("This command always fails");
        }
    }

    class ExampleSettings : IProcessingSettings
    {
        public int MaxConcurrentCalls => 5;
        public int PrefetchCount => 5;
        public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
        public int DeadLetterDeliveryLimit => 2;
    }
}
