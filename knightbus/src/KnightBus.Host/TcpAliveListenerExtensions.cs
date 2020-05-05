using KnightBus.Core;

namespace KnightBus.Host
{
    public static class TcpAliveListenerExtensions
    {
        public static IHostConfiguration UseTcpAliveListener(this IHostConfiguration configuration, int port)
        {
            var listener = new TcpAliveListenerPlugin(configuration, port);
            configuration.AddPlugin(listener);
            return configuration;
        }
    }
}
