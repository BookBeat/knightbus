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

        public static IServiceCollection UseJetStream(this IServiceCollection services, Action<IJetStreamConfiguration> configuration = null)
        {
            var jetStreamConfiguration = new JetStreamConfiguration();
            configuration?.Invoke(jetStreamConfiguration);
            services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory());
            services.AddSingleton<IJetStreamConfiguration>(jetStreamConfiguration);
            services.AddScoped<IJetStreamBus, JetStreamBus>();
            return services;
        }
    }
}
