using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;

[assembly: InternalsVisibleTo("KnightBus.LavinMQ.Tests.Unit")]

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
        var factory = BuildConnectionFactory(configuration);
        // The connection is a process-wide singleton created once at startup; blocking here is acceptable.
        return factory.CreateConnectionAsync().GetAwaiter().GetResult();
    }

    internal static ConnectionFactory BuildConnectionFactory(ILavinMQConfiguration configuration)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(configuration.ConnectionString),
            // Recover the connection, channels, consumers and declared topology automatically so a
            // dropped broker connection does not require a host restart.
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
        };
        configuration.ConfigureConnectionFactory?.Invoke(factory);
        return factory;
    }
}
