using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("KnightBus.DependencyInjection.Tests.Unit")]

namespace KnightBus.Core.DependencyInjection;

public static class MicrosoftDependencyInjectionExtensions
{
    /// <summary>
    /// Registers all <see cref="IProcessCommand{T,TSettings}"/>, <see cref="IProcessRequest{T,TResponse,TSettings}"/> and <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/> found in the specified assembly
    /// </summary>
    public static IServiceCollection RegisterProcessors(
        this IServiceCollection services,
        Assembly assembly
    )
    {
        foreach (var processorType in ValidProcessorInterfaces.Types)
        {
            foreach (
                var command in ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(
                    processorType,
                    assembly
                )
            )
            {
                RegisterGenericProcessor(services, command, processorType);
            }
        }
        return services;
    }

    /// <summary>
    /// Registers all <see cref="IProcessCommand{T,TSettings}"/>, <see cref="IProcessRequest{T,TResponse,TSettings}"/> and <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/> found in the executing assembly
    /// </summary>
    public static IServiceCollection RegisterProcessors(this IServiceCollection services)
    {
        return services.RegisterProcessors(Assembly.GetCallingAssembly());
    }

    public static IServiceCollection RegisterProcessor<T>(this IServiceCollection services)
    {
        foreach (var processorType in ValidProcessorInterfaces.Types)
        {
            RegisterGenericProcessor(services, typeof(T), processorType);
        }
        return services;
    }

    public static void RegisterGenericProcessor(
        this IServiceCollection services,
        Type implementingType,
        Type openGenericType
    )
    {
        var interfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(
            implementingType,
            openGenericType
        );
        foreach (var @interface in interfaces)
        {
            services.AddScoped(@interface, implementingType);
            services.AddScoped(
                typeof(IGenericProcessor),
                provider => provider.GetRequiredService(@interface)
            );
        }
    }

    public static void RegisterGenericProcessor(
        this IServiceCollection services,
        Type openGenericType,
        Assembly assembly
    )
    {
        var interfaces = ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(
            openGenericType,
            assembly
        );
        foreach (var @interface in interfaces)
        {
            RegisterGenericProcessor(services, @interface, openGenericType);
        }
    }
}
