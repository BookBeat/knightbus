using KnightBus.Core;
using SimpleInjector;

namespace KnightBus.SimpleInjector
{
    public static class SimpleInjectorExtensions
    {
        public static IHostConfiguration UseSimpleInjector(this IHostConfiguration configuration, Container container)
        {
            configuration.MessageProcessorProvider = new SimpleInjectorMessageProcessorProvider(container);
            configuration.Middlewares.Add(new SimpleInjectorScopedLifeStyleMiddleware(container));
            return configuration;
        }
    }
}