using KnightBus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Host
{

    public static class TcpAliveListenerExtensions
    {
        public static IServiceCollection UseTcpAliveListener(this IServiceCollection collection, int port)
        {
            collection.AddSingleton<ITcpAliveListenerConfiguration>(_ => new TcpAliveListenerConfiguration(port));
            collection.AddPlugin<TcpAliveListenerPlugin>();
            return collection;
        }

        public static IServiceCollection UseTcpAliveListener(this IServiceCollection collection,
            ITcpAliveListenerConfiguration configuration)
        {
            collection.AddSingleton(_ => configuration);
            collection.AddPlugin<TcpAliveListenerPlugin>();
            return collection;
        }
    }
}
