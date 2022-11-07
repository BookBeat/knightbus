using System;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client;

namespace KnightBus.Nats
{
    public static class NatsExtensions
    {
        public static IServiceCollection UseNats(this IServiceCollection services, Action<INatsConfiguration> configuration = null)
        {
            var natsConfiguration = new NatsConfiguration();
            configuration?.Invoke(natsConfiguration);
            services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory());
            services.AddSingleton<INatsConfiguration>(natsConfiguration);
            services.AddScoped<INatsBus, NatsBus>();
            return services;
        }
    }
}