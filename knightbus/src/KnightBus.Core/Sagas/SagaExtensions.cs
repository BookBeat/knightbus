using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Core.Sagas;

public static class SagaExtensions
{
    public static IServiceCollection EnableSagas<T>(this IServiceCollection services) where T : class, ISagaStore
    {
        if (services.Any(s => s.ServiceType == typeof(ISagaStore)))
        {
            throw new ArgumentException("An instance of ISagaStore is already registered");
        }

        services.AddSingleton<ISagaStore, T>();
        services.AddMiddleware<SagaMiddleware>();
        return services;
    }
    public static IServiceCollection EnableSagas(this IServiceCollection services, ISagaStore store)
    {
        if (services.Any(s => s.ServiceType == typeof(ISagaStore)))
        {
            throw new ArgumentException("An instance of ISagaStore is already registered");
        }

        services.AddSingleton(store);
        services.AddMiddleware<SagaMiddleware>();
        return services;
    }
}
