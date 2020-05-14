using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Host
{
    public static class KnightBusHostExtensions
    {
        public static IHostBuilder UseKnightBus(this IHostBuilder builder, KnightBusHost knightBus)
        {
            builder.UseConsoleLifetime()
                .ConfigureServices(collection => collection.AddHostedService(x => knightBus));

            return builder;
        }
    }
}