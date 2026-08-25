using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host.MessageProcessing;
using KnightBus.Host.Singleton;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host;

public class KnightBusHost : IHostedService
{
    private IHostConfiguration _configuration;
    private MessageProcessorLocator _locator;
    private readonly CancellationTokenSource _shutdownToken = new CancellationTokenSource();
    private readonly CancellationTokenSource _teardownToken = new CancellationTokenSource();
    private static readonly TimeSpan DrainPollingInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MinimumTeardownBudget = TimeSpan.FromSeconds(5);
    internal InFlightMessageTracker InFlightTracker { get; }
    internal List<IChannelReceiver> Receivers { get; } = new();
    internal List<IPlugin> Plugins { get; } = new();
    internal CancellationToken TeardownToken => _teardownToken.Token;

    public KnightBusHost(
        IHostConfiguration configuration,
        IServiceProvider provider,
        ILogger<KnightBusHost> logger
    )
    {
        configuration.DependencyInjection = new MicrosoftDependencyInjection(provider);
        configuration.Log = logger;
        _configuration = configuration;
        //The same instance the pipelines get as a middleware, see UseKnightBus
        InFlightTracker =
            provider.GetService<InFlightMessageTracker>() ?? new InFlightMessageTracker();
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
                _teardownToken.Token
            );
            Receivers.AddRange(_locator.CreateReceivers());
            _configuration.Log.LogInformation("Starting receivers");
            foreach (var receiver in Receivers)
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
            Plugins.Add(plugin);
        }
        _configuration.Log.LogInformation("KnightBus started");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _configuration.Log.LogInformation(
            "KnightBus received stop signal, initiating shutdown... "
        );
        _shutdownToken.Cancel();

        //Signal stoppable plugins right away so they stop accepting new work while the
        //pipeline drains, their completion is awaited after the drain
        var pluginStops = Plugins
            .OfType<IStoppablePlugin>()
            .Select(plugin => StopPluginAsync(plugin, cancellationToken))
            .ToArray();

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

        //Phase two: the pipeline is idle, release the singleton locks and wait for the
        //releases so the next instance can take over immediately without overlapping this
        //one, and wait for the stopping plugins to finish their in-flight work
        await _teardownToken.CancelAsync().ConfigureAwait(false);
        var teardowns = Receivers
            .OfType<SingletonChannelReceiver>()
            .Select(receiver => receiver.TeardownCompletion)
            .Concat(pluginStops)
            .ToArray();
        if (teardowns.Length > 0)
        {
            var teardownBudget = _configuration.ShutdownGracePeriod - stopWatch.Elapsed;
            if (teardownBudget < MinimumTeardownBudget)
                teardownBudget = MinimumTeardownBudget;
            try
            {
                await Task.WhenAll(teardowns)
                    .WaitAsync(teardownBudget, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException e)
            {
                _configuration.Log.LogWarning(
                    e,
                    "KnightBus shutdown proceeding before all singleton locks were released and plugins stopped"
                );
            }
            catch (OperationCanceledException)
            {
                //The runtime host gave up waiting for the shutdown
            }
        }

        _configuration.Log.LogInformation("KnightBus shutdown completed");
        _shutdownToken.Dispose();
        _teardownToken.Dispose();
    }

    private async Task StopPluginAsync(IStoppablePlugin plugin, CancellationToken cancellationToken)
    {
        try
        {
            await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            //The runtime host gave up waiting for the shutdown
        }
        catch (Exception e)
        {
            //A plugin failing to stop must not fail the shutdown of everything else
            _configuration.Log.LogWarning(
                e,
                "Failed to stop plugin {PluginType}",
                plugin.GetType()
            );
        }
    }

    public async Task StartAndBlockAsync(CancellationToken cancellationToken)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.WaitHandle.WaitOne();
    }
}
