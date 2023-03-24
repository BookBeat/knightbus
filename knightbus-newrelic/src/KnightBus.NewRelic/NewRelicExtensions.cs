using KnightBus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.NewRelicMiddleware
{
    public static class NewRelicExtensions
    {
        public static IServiceCollection UseNewRelic(this IServiceCollection services)
        {
            services.AddMiddleware<NewRelicMessageMiddleware>();
            return services;
        }
    }
}
