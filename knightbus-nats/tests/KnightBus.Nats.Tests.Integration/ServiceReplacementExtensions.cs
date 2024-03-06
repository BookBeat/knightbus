using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Nats.Tests.Integration;

public static class ServiceReplacementExtensions
{
    public static IServiceCollection Replace<TService>(this IServiceCollection services)
        where TService : class
    {
        services.AddSingleton<ServiceReplacement<TService>>();
        services.AddScoped(provider => provider.GetService<ServiceReplacement<TService>>().Service);
        return services;
    }
}
