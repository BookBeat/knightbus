using System.Collections.Generic;
using System.Reflection;
using KnightBus.Core;
using SimpleInjector;

namespace KnightBus.SimpleInjector
{
    public static class SimpleInjectorExtensions
    {
        /// <summary>
        /// Enables SimpleInjector by using <see cref="SimpleInjectorDependencyInjection"/> and adding the <see cref="SimpleInjectorScopedLifeStyleMiddleware"/> enabling scoped per message
        /// </summary>
        public static IHostConfiguration UseSimpleInjector(this IHostConfiguration configuration, Container container)
        {
            configuration.DependencyInjection = new SimpleInjectorDependencyInjection(container);
            configuration.Middlewares.Add(new SimpleInjectorScopedLifeStyleMiddleware(container));
            return configuration;
        }

        /// <summary>
        /// Registers all <see cref="IProcessCommand{T,TSettings}"/>, <see cref="IProcessRequest{T,TResponse,TSettings}"/> and <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/> found in the executing assembly
        /// </summary>
        public static IHostConfiguration RegisterProcessors(this IHostConfiguration configuration, Container container, Assembly assembly)
        {
            foreach (var processorType in ValidProcessorInterfaces.Types)
            {
                container.Register(processorType, new List<Assembly> { assembly }, Lifestyle.Scoped);
            }
            return configuration;
        }
    }
}