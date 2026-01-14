using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.ServiceBus;
using KnightBus.Azure.ServiceBus.Management;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.DistributedTracing;
using KnightBus.Host;
using KnightBus.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace KnightBus.Examples.Azure.ServiceBus;

class Program
{
    static async Task Main(string[] args)
    {
        var knightBus = Microsoft
            .Extensions.Hosting.Host.CreateDefaultBuilder(args)
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureLogging(logging =>
            {
                logging.AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                    options.ParseStateValues = true;
                });
            })
            .ConfigureServices(
                (context, services) =>
                {
                    // Get connection string from Aspire configuration
                    var connectionString = context.Configuration.GetConnectionString("servicebus");

                    var serviceBusConfiguration = new ServiceBusConfiguration
                    {
                        ConnectionString = connectionString,
                    };

                    // Configure OpenTelemetry tracing
                    // This sets up the OpenTelemetry SDK with console exporter for demo purposes
                    services
                        .AddOpenTelemetry()
                        .ConfigureResource(resourceBuilder =>
                        {
                            resourceBuilder.AddService("KnightBus.Examples.Azure.ServiceBus");
                        })
                        .UseOtlpExporter()
                        .WithTracing(builder =>
                        {
                            builder.SetSampler(new AlwaysOnSampler());
                            builder
                                .AddAspNetCoreInstrumentation()
                                .AddHttpClientInstrumentation()
                                .AddConsoleExporter()
                                // Register KnightBus ActivitySource for tracing
                                .AddKnightBusInstrumentation();
                        })
                        .WithMetrics(meterProviderBuilder =>
                        {
                            meterProviderBuilder
                                .AddHttpClientInstrumentation()
                                .AddProcessInstrumentation()
                                .AddRuntimeInstrumentation();
                        });

                    services
                        .UseDistributedTracing()
                        .UseServiceBus(serviceBusConfiguration)
                        .AddServiceBusManagement(serviceBusConfiguration)
                        .RegisterProcessors(typeof(SampleServiceBusEventProcessor).Assembly)
                        .UseOpenTelemetry()
                        .UseTransport<ServiceBusTransport>();
                }
            )
            .UseKnightBus()
            .Build();

        //Initiate the client
        var client = (KnightBus.Azure.ServiceBus.ServiceBus)
            knightBus.Services.CreateScope().ServiceProvider.GetRequiredService<IServiceBus>();

        var managementClient = knightBus
            .Services.CreateScope()
            .ServiceProvider.GetRequiredService<ServiceBusQueueManager>();

        //Start the KnightBus Host, it will now connect to the ServiceBus and listen to the SampleServiceBusMessageMapping.QueueName
        await knightBus.RunAsync();
    }

    class SampleServiceBusMessageProcessor(ILogger<SampleServiceBusMessageProcessor> logger)
        : IProcessCommand<SampleServiceBusMessage, SomeProcessingSetting>,
            IProcessCommand<OtherSampleServiceBusMessage, SomeProcessingSetting>,
            IProcessEvent<SampleServiceBusEvent, EventSubscriptionOne, SomeProcessingSetting>
    {
        public async Task ProcessAsync(
            SampleServiceBusMessage message,
            CancellationToken cancellationToken
        )
        {
            var response = await new HttpClient().GetStringAsync("https://www.google.com");
            logger.LogInformation($"Received command: '{message.Message}'");
        }

        public Task ProcessAsync(SampleServiceBusEvent message, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Received event: '{message.Message}'");
            return Task.CompletedTask;
        }

        public async Task ProcessAsync(
            OtherSampleServiceBusMessage message,
            CancellationToken cancellationToken
        )
        {
            var response = await new HttpClient().GetStringAsync("https://www.google.com");
            logger.LogInformation($"Received command: '{message.SomeProperty}'");
        }
    }

    class SampleServiceBusEventProcessor(ILogger<SampleServiceBusEventProcessor> logger)
        : IProcessEvent<SampleServiceBusEvent, EventSubscriptionTwo, SomeProcessingSetting>,
            IProcessBeforeDeadLetter<SampleServiceBusEvent>
    {
        public Task ProcessAsync(SampleServiceBusEvent message, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Also received event: '{message.Message}'");
            throw new Exception("Trigger retry");
        }

        public Task BeforeDeadLetterAsync(
            SampleServiceBusEvent message,
            CancellationToken cancellationToken
        )
        {
            logger.LogInformation($"Dead lettering event: '{message.Message}'");
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
