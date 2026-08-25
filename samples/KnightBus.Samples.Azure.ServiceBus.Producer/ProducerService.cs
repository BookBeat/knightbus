using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.ServiceBus;
using KnightBus.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnightBus.Examples.Azure.ServiceBus.Producer;

public class ProducerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProducerService> _logger;

    public ProducerService(IServiceProvider serviceProvider, ILogger<ProducerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting message producer...");

            // Create a scope to resolve scoped services
            using var scope = _serviceProvider.CreateScope();
            var serviceBus = scope.ServiceProvider.GetRequiredService<IServiceBus>();

            var batchNumber = 0;

            // Keep sending batches until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                batchNumber++;
                _logger.LogInformation("Sending batch {BatchNumber}...", batchNumber);

                // Send commands
                for (var i = 0; i < 10; i++)
                {
                    using var activity = KnightBusDiagnostics.ActivitySource.StartActivity(
                        "Send SampleServiceBusMessage",
                        ActivityKind.Producer
                    );
                    await serviceBus.SendAsync(
                        new SampleServiceBusMessage
                        {
                            Message = $"Batch {batchNumber}, Command {i}",
                        },
                        stoppingToken
                    );
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    _logger.LogInformation(
                        "Sent batch {BatchNumber}, command {Index}",
                        batchNumber,
                        i
                    );
                }

                // Send events
                _logger.LogInformation("Sending events...");
                for (var i = 0; i < 10; i++)
                {
                    using var activity = KnightBusDiagnostics.ActivitySource.StartActivity(
                        "Publish SampleServiceBusEvent",
                        ActivityKind.Producer
                    );
                    await serviceBus.PublishEventAsync(
                        new SampleServiceBusEvent { Message = $"Hello from event {i}" }
                    );
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    _logger.LogInformation("Published event {Index}", i);
                }

                _logger.LogInformation(
                    "Batch {BatchNumber} complete. Waiting 5 seconds...",
                    batchNumber
                );

                // Wait 5 seconds before sending the next batch
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Producer service stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in producer service");
            throw;
        }
    }
}
