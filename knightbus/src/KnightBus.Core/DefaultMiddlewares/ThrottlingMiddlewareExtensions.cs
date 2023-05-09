using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Core.DefaultMiddlewares
{
    public static class ThrottlingMiddlewareExtensions
    {
        public static IServiceCollection ThrottleHost(this IServiceCollection services, int maxConcurrent)
        {
            services.AddMiddleware(new ThrottlingMiddleware(maxConcurrent));
            return services;
        }
    }
}
