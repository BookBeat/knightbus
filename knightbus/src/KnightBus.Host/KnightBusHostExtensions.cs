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
            //.ConfigureHostOptions(host => host.ShutdownTimeout = conf.ShutdownGracePeriod.Add(TimeSpan.FromSeconds(10)))
            .ConfigureServices(collection =>
            {
                collection.AddSingleton(conf);
                collection.AddHostedService<KnightBusHost>();
            });

        return builder;
    }
}
