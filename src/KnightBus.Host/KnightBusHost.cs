using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.Singleton;
using KnightBus.Host.MessageProcessing;
using KnightBus.Host.Singleton;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host;

public class KnightBusHost : IHostedService
{
    private IHostConfiguration _configuration;
    private readonly CancellationTokenSource _shutdownToken = new CancellationTokenSource();
    private readonly CancellationTokenSource _teardownToken = new CancellationTokenSource();
    private static readonly TimeSpan DrainPollingInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MinimumTeardownBudget = TimeSpan.FromSeconds(5);
    internal InFlightMessageTracker InFlightTracker { get; }
    internal List<IChannelReceiver> Receivers { get; } = new();
    internal List<IPlugin> Plugins { get; } = new();
    private ISingletonPlacement? _placement;
    private static readonly TimeSpan LeaveOnFailedStartTimeout = TimeSpan.FromSeconds(10);
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

        try
        {
            var transports = _configuration
                .DependencyInjection.GetInstances<ITransport>()
                .ToArray();

            if (transports.Any())
            {
                //The receivers and the host share one placement. Without a membership the singleton
                //receivers compete for the locks as they always did
                var membership = _configuration
                    .DependencyInjection.GetInstances<ISingletonMembership>()
                    .LastOrDefault();
                if (membership != null)
                    _placement = new SingletonPlacement(membership, _configuration.Log);
                var locator = new MessageProcessorLocator(
                    _configuration,
                    transports
                        .SelectMany(transport => transport.TransportChannelFactories)
                        .ToArray(),
                    _teardownToken.Token,
                    _placement
                );
                Receivers.AddRange(locator.CreateReceivers());

                //Join the group before the receivers start, so they get advice on which locks to take
                //A placement that cannot join keeps trying by itself, only a cancelled start ends it
                if (_placement != null)
                {
                    var locks = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (var singleton in Receivers.OfType<SingletonChannelReceiver>())
                        locks[singleton.LockId] = singleton.Weight;
                    await _placement.StartAsync(locks, combinedToken.Token).ConfigureAwait(false);
                }
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
        }
        catch
        {
            //A host that failed to start must not stay a member, or the others keep giving it locks
            using var leaving = new CancellationTokenSource(LeaveOnFailedStartTimeout);
            await LeavePlacementAsync(leaving.Token).ConfigureAwait(false);
            throw;
        }
        _configuration.Log.LogInformation("KnightBus started");
    }

    private async Task LeavePlacementAsync(CancellationToken cancellationToken)
    {
        if (_placement == null)
            return;
        try
        {
            await _placement.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
            when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _configuration.Log.LogWarning(e, "Could not leave the singleton placement group");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _configuration.Log.LogInformation(
            "KnightBus received stop signal, initiating shutdown... "
        );
        _shutdownToken.Cancel();

        //Leave the group right away: the other hosts can then start counting on the locks this one
        //is about to release, instead of leaving them alone for the grace period. It runs alongside
        //the drain, so slow storage does not hold the rest of the shutdown up
        var leaving = LeavePlacementAsync(cancellationToken);

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
            .Append(leaving)
            .ToArray();
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
