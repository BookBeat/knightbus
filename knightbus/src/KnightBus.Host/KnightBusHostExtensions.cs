using System;
using KnightBus.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Host;

public static class KnightBusHostExtensions
{
    public static IHostBuilder UseKnightBus(
        this IHostBuilder builder,
        Action<IHostConfiguration> configuration = null
    )
    {
        IHostConfiguration conf = new HostConfiguration();
        configuration?.Invoke(conf);
        builder
            .UseConsoleLifetime()
            //Give KnightBus room to drain in-flight messages before the runtime host aborts the shutdown
            .ConfigureHostOptions(host =>
                host.ShutdownTimeout = conf.ShutdownGracePeriod.Add(TimeSpan.FromSeconds(10))
            )
            .ConfigureServices(collection =>
            {
                collection.AddSingleton(conf);
                //One tracker for the whole host, reaching every pipeline as a middleware so
                //that shutdown can see all in flight messages through a single counter
                collection.AddSingleton<InFlightMessageTracker>();
                collection.AddSingleton<IMessageProcessorMiddleware>(provider =>
                    provider.GetRequiredService<InFlightMessageTracker>()
                );
                collection.AddHostedService<KnightBusHost>();
            });

        return builder;
    }
}
