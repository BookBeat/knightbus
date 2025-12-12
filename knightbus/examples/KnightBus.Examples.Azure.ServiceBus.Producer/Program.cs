using System.Threading.Tasks;
using KnightBus.Azure.ServiceBus;
using KnightBus.Core.DistributedTracing;
using KnightBus.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace KnightBus.Examples.Azure.ServiceBus.Producer;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
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

                    // Configure OpenTelemetry tracing for outgoing messages
                    services
                        .AddOpenTelemetry()
                        .ConfigureResource(resourceBuilder =>
                        {
                            resourceBuilder.AddService(
                                "KnightBus.Examples.Azure.ServiceBus.Producer"
                            );
                        })
                        .UseOtlpExporter()
                        .WithTracing(builder =>
                        {
                            builder
                                .AddHttpClientInstrumentation()
                                .AddHttpClientInstrumentation()
                                .AddKnightBusInstrumentation()
                                .AddConsoleExporter();
                        })
                        .WithMetrics(meterProviderBuilder =>
                        {
                            meterProviderBuilder
                                .AddHttpClientInstrumentation()
                                .AddProcessInstrumentation()
                                .AddRuntimeInstrumentation()
                                .AddConsoleExporter();
                        });

                    // Register KnightBus ServiceBus client
                    services.UseDistributedTracing();
                    services.UseServiceBus(serviceBusConfiguration);
                    services.UseOpenTelemetry();

                    // Register the producer background service
                    services.AddHostedService<ProducerService>();
                }
            )
            .Build();

        await host.RunAsync();
    }
}
