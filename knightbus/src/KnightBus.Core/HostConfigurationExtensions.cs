using KnightBus.Core.Singleton;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KnightBus.Core
{
    public static class HostConfigurationExtensions
    {

        public static IHostConfiguration UseTransport(this IHostConfiguration configuration, ITransport transport)
        {
            configuration.Transports.Add(transport);
            return configuration;
        }
        public static IHostConfiguration UseLog(this IHostConfiguration configuration, ILogger log)
        {
            configuration.Log = log;
            return configuration;
        }
        public static IHostConfiguration AddMiddleware(this IHostConfiguration configuration, IMessageProcessorMiddleware middleware)
        {
            configuration.Middlewares.Add(middleware);
            return configuration;
        }

        public static IServiceCollection AddPlugin<T>(this IServiceCollection collection) where T : class, IPlugin
        {
            collection.AddSingleton<IPlugin, T>();
            return collection;
        }

        public static IServiceCollection UseSingletonLocks(this IServiceCollection collection, ISingletonLockManager lockManager)
        {
            collection.AddSingleton(lockManager);
            return collection;
        }
        public static IHostConfiguration UseDependencyInjection(this IHostConfiguration configuration, IDependencyInjection provider)
        {
            configuration.DependencyInjection = provider;
            return configuration;
        }
    }
}