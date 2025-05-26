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
        var configuration = new ServiceBusConfiguration();
        config?.Invoke(configuration);
        collection.AddSingleton<IServiceBusConfiguration>(configuration);
        collection.AddSingleton<IClientFactory, ClientFactory>();
        collection.AddScoped<IServiceBus, ServiceBus>();
        return collection;
    }
}
