using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host.MessageProcessing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host;

public class KnightBusHost : IHostedService
{
    private IHostConfiguration _configuration;
    private MessageProcessorLocator _locator;
    private readonly CancellationTokenSource _shutdownToken = new CancellationTokenSource();
    private static readonly TimeSpan DrainPollingInterval = TimeSpan.FromMilliseconds(100);
    internal InFlightMessageTracker InFlightTracker { get; } = new();

    public KnightBusHost(
        IHostConfiguration configuration,
        IServiceProvider provider,
        ILogger<KnightBusHost> logger
    )
    {
        configuration.DependencyInjection = new MicrosoftDependencyInjection(provider);
        configuration.Log = logger;
        _configuration = configuration;
    }

    public KnightBusHost Configure(Func<IHostConfiguration, IHostConfiguration> configuration)
    {
        _configuration = configuration(_configuration);
        return this;
    }

    /// <summary>
    /// Starts the bus and wires all listeners
    /// </summary>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdownToken.Token
        );
        _configuration.Log.LogInformation("KnightBus starting");

        var transports = _configuration.DependencyInjection.GetInstances<ITransport>().ToArray();

        if (transports.Any())
        {
            _locator = new MessageProcessorLocator(
                _configuration,
                transports.SelectMany(transport => transport.TransportChannelFactories).ToArray(),
                InFlightTracker
            );
            var channelReceivers = _locator.CreateReceivers().ToList();
            _configuration.Log.LogInformation("Starting receivers");
            foreach (var receiver in channelReceivers)
            {
                _configuration.Log.LogInformation(
                    "Starting receiver {ReceiverType}",
                    receiver.GetType()
                );
                await receiver.StartAsync(combinedToken.Token).ConfigureAwait(false);
            }

            _configuration.Log.LogInformation("Finished starting receivers");
        }
        else
        {
            _configuration.Log.LogInformation("No transports found");
        }

        foreach (var plugin in _configuration.DependencyInjection.GetInstances<IPlugin>())
        {
            await plugin.StartAsync(combinedToken.Token).ConfigureAwait(false);
        }
        _configuration.Log.LogInformation("KnightBus started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _configuration.Log.LogInformation(
            "KnightBus received stop signal, initiating shutdown... "
        );
        _shutdownToken.Cancel();

        //Wait for in-flight messages to drain instead of always waiting the full grace period.
        //Always wait at least one interval so just-dispatched messages get counted.
        var stopWatch = Stopwatch.StartNew();
        do
        {
            try
            {
                await Task.Delay(DrainPollingInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                //The runtime host gave up waiting for the shutdown
                break;
            }
        } while (
            InFlightTracker.Count > 0 && stopWatch.Elapsed < _configuration.ShutdownGracePeriod
        );

        if (InFlightTracker.Count > 0)
        {
            _configuration.Log.LogWarning(
                "KnightBus shutdown proceeding with {MessageCount} messages still processing",
                InFlightTracker.Count
            );
        }
        _configuration.Log.LogInformation("KnightBus shutdown completed");
        _shutdownToken.Dispose();
    }

    public async Task StartAndBlockAsync(CancellationToken cancellationToken)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.WaitHandle.WaitOne();
    }
}
