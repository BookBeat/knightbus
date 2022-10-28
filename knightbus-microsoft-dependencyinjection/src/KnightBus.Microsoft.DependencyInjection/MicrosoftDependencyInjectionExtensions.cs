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
        /// Enables dependency injection by using <see cref="MicrosoftDependencyInjectionExtensions"/> and adding the <see cref="MicrosoftDependencyInjectionScopedLifeStyleMiddleware"/> enabling scoped per message
        /// </summary>
        public static IHostConfiguration UseMicrosoftDependencyInjection(this IHostConfiguration configuration, IServiceProvider provider)
        {
            configuration.DependencyInjection = new MicrosoftDependencyInjection(provider);
            configuration.Middlewares.Add(new MicrosoftDependencyInjectionScopedLifeStyleMiddleware());
            return configuration;
        }

        /// <summary>
        /// Registers all <see cref="IProcessCommand{T,TSettings}"/>, <see cref="IProcessRequest{T,TResponse,TSettings}"/> and <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/> found in the executing assembly
        /// </summary>
        public static IHostConfiguration RegisterProcessors(this IHostConfiguration configuration, IServiceCollection serviceCollection, Assembly assembly)
        {
            serviceCollection.RegisterProcessors(assembly);
            return configuration;
        }

        /// <summary>
        /// Registers all <see cref="IProcessCommand{T,TSettings}"/>, <see cref="IProcessRequest{T,TResponse,TSettings}"/> and <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/> found in the executing assembly
        /// </summary>
        public static IServiceCollection RegisterProcessors(this IServiceCollection serviceCollection, Assembly assembly)
        {
            foreach (var processorType in ValidProcessorInterfaces.Types)
            {
                foreach (var command in ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(processorType, assembly))
                {
                    RegisterGenericProcessor(serviceCollection, command, processorType);
                }
            }
            return serviceCollection;
        }
        
        public static IServiceCollection RegisterProcessor<T>(this IServiceCollection serviceCollection)
        {
            foreach (var processorType in ValidProcessorInterfaces.Types)
            {
                RegisterGenericProcessor(serviceCollection, typeof(T), processorType);
            }
            return serviceCollection;
        }

        public static void RegisterGenericProcessor(this IServiceCollection serviceCollection, Type implementingType, Type openGenericType)
        {
            var interfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(implementingType, openGenericType);
            foreach (var @interface in interfaces)
            {
                serviceCollection.AddScoped(@interface, implementingType);
                serviceCollection.AddScoped(typeof(IGenericProcessor), provider => provider.GetRequiredService(@interface));
            }
        }
        public static void RegisterGenericProcessor(this IServiceCollection serviceCollection, Type openGenericType, Assembly assembly)
        {
            var interfaces = ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(openGenericType, assembly);
            foreach (var @interface in interfaces)
            {
                RegisterGenericProcessor(serviceCollection, @interface, openGenericType);
            }
        }
    }
}
