using System;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Azure.ServiceBus
{

    public static class ServiceBusExtensions
    {
        public static IServiceCollection UseServiceBusClient(this IServiceCollection collection, string connectionString, Action<IServiceBusConfiguration> config = null)
        {
            var configuration = new ServiceBusConfiguration(connectionString);
            config?.Invoke(configuration);
            collection.AddSingleton<IServiceBusConfiguration>(configuration);
            collection.AddSingleton<IServiceBus, ServiceBus>();
            return collection;
        }
    }
}