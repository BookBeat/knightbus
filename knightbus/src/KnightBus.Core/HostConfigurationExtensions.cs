using KnightBus.Core.Singleton;

namespace KnightBus.Core
{
    public static class HostConfigurationExtensions
    {
        public static IHostConfiguration UseLog(this IHostConfiguration configuration, ILog log)
        {
            configuration.Log = log;
            return configuration;
        }
        public static IHostConfiguration AddMiddleware(this IHostConfiguration configuration, IMessageProcessorMiddleware middleware)
        {
            configuration.Middlewares.Add(middleware);
            return configuration;
        }

        public static IHostConfiguration AddPlugin(this IHostConfiguration configuration, IPlugin plugin)
        {
            configuration.Plugins.Add(plugin);
            return configuration;
        }

        public static IHostConfiguration UseSingletonLocks(this IHostConfiguration configuration, ISingletonLockManager lockManager)
        {
            configuration.SingletonLockManager = lockManager;
            return configuration;
        }
        public static IHostConfiguration UseDependencyInjection(this IHostConfiguration configuration, IDependencyInjection provider)
        {
            configuration.DependencyInjection = provider;
            return configuration;
        }
    }
}