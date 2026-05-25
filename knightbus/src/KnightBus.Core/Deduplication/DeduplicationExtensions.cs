using System;
using System.Linq;
using KnightBus.Core.PreProcessors;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Core.Deduplication;

public static class DeduplicationExtensions
{
    public static IServiceCollection EnableDeduplication<T>(this IServiceCollection services)
        where T : class, IDeduplicationStore
    {
        if (services.Any(s => s.ServiceType == typeof(IDeduplicationStore)))
        {
            throw new ArgumentException("An instance of IDeduplicationStore is already registered");
        }

        services.AddSingleton<IDeduplicationStore, T>();
        services.AddSingleton<IMessagePreProcessor, DeduplicationPreProcessor>();
        services.AddMiddleware<DeduplicationMiddleware>();
        return services;
    }

    public static IServiceCollection EnableDeduplication(
        this IServiceCollection services,
        IDeduplicationStore store
    )
    {
        if (services.Any(s => s.ServiceType == typeof(IDeduplicationStore)))
        {
            throw new ArgumentException("An instance of IDeduplicationStore is already registered");
        }

        services.AddSingleton(store);
        services.AddSingleton<IMessagePreProcessor, DeduplicationPreProcessor>();
        services.AddMiddleware<DeduplicationMiddleware>();
        return services;
    }
}
