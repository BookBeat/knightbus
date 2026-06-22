using System;
using KnightBus.Core.Management;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.LavinMQ.Management;

public static class LavinMQManagementExtensions
{
    /// <summary>
    /// Registers the LavinMQ <see cref="IQueueManager"/> and the underlying transport
    /// (configuration + shared connection). The management HTTP endpoint and credentials are derived
    /// from the AMQP connection string.
    /// </summary>
    public static IServiceCollection UseLavinMQManagement(
        this IServiceCollection services,
        Action<ILavinMQConfiguration>? configuration = null
    )
    {
        services.AddSingleton<LavinMQQueueManager>();
        services.AddSingleton<IQueueManager>(provider =>
            provider.GetRequiredService<LavinMQQueueManager>()
        );
        return services.UseLavinMQ(configuration);
    }
}
