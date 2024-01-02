using KnightBus.Core.PreProcessors;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Core.DistributedTracing
{
    public static class DistributedTracingUtility
    {
        public const string TraceIdKey = "_traceid";

        public static IServiceCollection UseDistributedTracing<T>(this IServiceCollection services) where T : class, IDistributedTracingProvider
        {
            services.AddScoped<IDistributedTracingProvider, T>();
            services.AddMiddleware<DistributedTracingMiddleware>();
            services.AddScoped<IMessagePreProcessor, DistributedTracingPreProcessor>();
            return services;
        }
    }
}
