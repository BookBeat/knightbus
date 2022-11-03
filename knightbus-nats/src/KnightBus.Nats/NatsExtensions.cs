using System;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client;

namespace KnightBus.Nats
{
    public static class NatsExtensions
    {
        public static IServiceCollection UseNatsClient(this IServiceCollection services, string connectionString,
            Action<INatsBusConfiguration> configuration)
        {
            var natsConfiguration = new NatsBusConfiguration(connectionString);
            configuration?.Invoke(natsConfiguration);
            services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory());
            services.AddSingleton<INatsBusConfiguration>(natsConfiguration);
            services.AddSingleton<INatsBus, NatsBus>();
            return services;
        }
        public static IServiceCollection UseNatsClient(this IServiceCollection services, string connectionString)
        {
            return services.UseNatsClient(connectionString, _ => {});
        }
    }
}