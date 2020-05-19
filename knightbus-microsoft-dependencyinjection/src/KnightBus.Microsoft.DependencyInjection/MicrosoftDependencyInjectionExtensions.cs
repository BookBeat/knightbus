using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using KnightBus.Core;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("KnightBus.DependencyInjection.Tests.Unit")]
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
            serviceCollection.RegisterProcessors(assembly);
            return configuration;
        }

        /// <summary>
        /// Registers all <see cref="IProcessCommand{T,TSettings}"/> and <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/> found in the executing assembly
        /// </summary>
        public static IServiceCollection RegisterProcessors(this IServiceCollection serviceCollection, Assembly assembly)
        {
            foreach (var command in ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(typeof(IProcessCommand<,>), assembly))
                RegisterOpenGenericType(serviceCollection, command, typeof(IProcessCommand<,>));

            foreach (var events in ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(typeof(IProcessEvent<,,>), assembly))
                RegisterOpenGenericType(serviceCollection, events, typeof(IProcessEvent<,,>));

            return serviceCollection;
        }

        internal static void RegisterOpenGenericType(IServiceCollection serviceCollection, Type implementingType, Type openGenericType)
        {
            var processorInterfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(implementingType, openGenericType);
            foreach (var processorInterface in processorInterfaces)
            {
                serviceCollection.AddScoped(processorInterface, implementingType);
            }
        }
    }
}