using System;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Azure.ServiceBus;

public static class ServiceBusExtensions
{
    public static IServiceCollection UseServiceBus(
        this IServiceCollection collection,
        Action<IServiceBusConfiguration> config = null
    )
    {
        ArgumentNullException.ThrowIfNull(collection);

        return collection.UseServiceBus(_ =>
        {
            var configuration = new ServiceBusConfiguration();
            config?.Invoke(configuration);
            return configuration;
        });
    }

    public static IServiceCollection UseServiceBus(
        this IServiceCollection collection,
        Func<IServiceProvider, IServiceBusConfiguration> configurationFactory
    )
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(configurationFactory);

        collection.AddSingleton<IServiceBusConfiguration>(sp =>
        {
            var configuration = configurationFactory(sp);
            return configuration
                ?? throw new InvalidOperationException(
                    $"{nameof(configurationFactory)} must not return null."
                );
        });

        return AddServiceBus(collection);
    }

    public static IServiceCollection UseServiceBus(
        this IServiceCollection collection,
        IServiceBusConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(configuration);

        collection.AddSingleton<IServiceBusConfiguration>(configuration);
        return AddServiceBus(collection);
    }

    private static IServiceCollection AddServiceBus(IServiceCollection collection)
    {
        collection.AddSingleton<IClientFactory, ClientFactory>();
        collection.AddScoped<IServiceBus, ServiceBus>();
        return collection;
    }
}
