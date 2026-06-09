using KnightBus.Core;
using KnightBus.Core.DistributedTracing;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.NewRelicMiddleware;

public static class NewRelicExtensions
{
    public static IServiceCollection UseNewRelic(this IServiceCollection services)
    {
        services.AddMiddleware<NewRelicErrorHandlingMiddleware>();
        services.UseDistributedTracing<NewRelicDistributedTracingProvider>();
        return services;
    }
}
