using System;
using System.Reflection;
using KnightBus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Microsoft.DependencyInjection
{
    public static class MicrosoftDependencyInjectionExtensions
    {
        /// <summary>
        /// Enables SimpleInjector by using <see cref="MicrosoftDependencyInjectionExtensions"/> and adding the <see cref="MicrosoftDependencyInjectionScopedLifeStyleMiddleware"/> enabling scoped per message
        /// </summary>
        public static IHostConfiguration UseMicrosoftDependencyInjection(this IHostConfiguration configuration, IServiceProvider serviceProvider, IServiceCollection serviceCollection)
        {
            configuration.DependencyInjection = new MicrosoftDependencyInjection(serviceProvider, serviceCollection);
            configuration.Middlewares.Add(new MicrosoftDependencyInjectionScopedLifeStyleMiddleware(serviceProvider));
            return configuration;
        }

        /// <summary>
        /// Registers all <see cref="IProcessCommand{T,TSettings}"/> and <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/> found in the executing assembly
        /// </summary>
        public static IHostConfiguration RegisterProcessors(this IHostConfiguration configuration, IServiceCollection serviceCollection, Assembly assembly)
        {
            foreach (var command in ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(typeof(IProcessCommand<,>), assembly))
                serviceCollection.AddScoped(typeof(IProcessCommand<,>), command);
            
            foreach (var events in ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(typeof(IProcessEvent<,,>), assembly))
                serviceCollection.AddScoped(typeof(IProcessEvent<,,>), events);

            return configuration;
        }
    }
}