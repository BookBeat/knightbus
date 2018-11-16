using System.Collections.Generic;
using System.Reflection;
using KnightBus.Core;
using SimpleInjector;

namespace KnightBus.SimpleInjector
{
    public static class SimpleInjectorExtensions
    {
        /// <summary>
        /// Enables SimpleInjector by using <see cref="SimpleInjectorMessageProcessorProvider"/> and adding the <see cref="SimpleInjectorScopedLifeStyleMiddleware"/> enabling scoped per message
        /// </summary>
        public static IHostConfiguration UseSimpleInjector(this IHostConfiguration configuration, Container container)
        {
            configuration.MessageProcessorProvider = new SimpleInjectorMessageProcessorProvider(container);
            configuration.Middlewares.Add(new SimpleInjectorScopedLifeStyleMiddleware(container));
            return configuration;
        }

        /// <summary>
        /// Registers all <see cref="IProcessCommand{T,TSettings}"/> and <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/> found in the executing assembly
        /// </summary>
        public static IHostConfiguration RegisterProcessors(this IHostConfiguration configuration, Container container)
        {
            container.Register(typeof(IProcessCommand<,>), new List<Assembly> { Assembly.GetExecutingAssembly() }, Lifestyle.Scoped);
            container.Register(typeof(IProcessEvent<,,>), new List<Assembly> { Assembly.GetExecutingAssembly() }, Lifestyle.Scoped);
            return configuration;
        }
    }
}