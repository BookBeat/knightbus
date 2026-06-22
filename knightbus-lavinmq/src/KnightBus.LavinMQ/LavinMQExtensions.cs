using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ;

public static class LavinMQExtensions
{
    /// <summary>
    /// Registers the LavinMQ configuration, a shared AMQP <see cref="IConnection"/> and the
    /// <see cref="ILavinMQBus"/> client. Enable the transport with
    /// <c>UseTransport&lt;LavinMQTransport&gt;()</c>.
    /// </summary>
    public static IServiceCollection UseLavinMQ(
        this IServiceCollection services,
        Action<ILavinMQConfiguration>? configuration = null
    )
    {
        var lavinConfiguration = new LavinMQConfiguration();
        configuration?.Invoke(lavinConfiguration);

        // TryAdd so calling this more than once (e.g. UseLavinMQ + UseLavinMQManagement) keeps the
        // first registration's configuration/connection instead of clobbering it with a later empty one.
        services.TryAddSingleton<ILavinMQConfiguration>(lavinConfiguration);
        services.TryAddSingleton<IConnection>(provider =>
            CreateConnection(provider.GetRequiredService<ILavinMQConfiguration>())
        );
        services.TryAddScoped<ILavinMQBus, LavinMQBus>();
        return services;
    }

    private static IConnection CreateConnection(ILavinMQConfiguration configuration)
    {
        var factory = new ConnectionFactory { Uri = new Uri(configuration.ConnectionString) };
        // The connection is a process-wide singleton created once at startup; blocking here is acceptable.
        return factory.CreateConnectionAsync().GetAwaiter().GetResult();
    }
}
