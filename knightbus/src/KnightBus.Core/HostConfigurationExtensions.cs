using KnightBus.Core.Singleton;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Core
{
    public static class HostConfigurationExtensions
    {

        public static IServiceCollection UseTransport<T>(this IServiceCollection services) where T: class, ITransport
        {
            services.AddSingleton<ITransport, T>();
            return services;
        }
        public static IServiceCollection AddMiddleware<T>(this IServiceCollection services) where T: class, IMessageProcessorMiddleware
        {
            services.AddSingleton<IMessageProcessorMiddleware, T>();
            return services;
        }
        public static IServiceCollection AddMiddleware(this IServiceCollection services, IMessageProcessorMiddleware middleware)
        {
            services.AddSingleton(middleware);
            return services;
        }

        public static IServiceCollection AddPlugin<T>(this IServiceCollection services) where T : class, IPlugin
        {
            services.AddSingleton<IPlugin, T>();
            return services;
        }

        public static IServiceCollection UseSingletonLocks(this IServiceCollection services, ISingletonLockManager lockManager)
        {
            services.AddSingleton(lockManager);
            return services;
        }
    }
}