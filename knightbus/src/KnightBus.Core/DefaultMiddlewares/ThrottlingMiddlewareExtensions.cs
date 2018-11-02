namespace KnightBus.Core.DefaultMiddlewares
{
    public static class ThrottlingMiddlewareExtensions
    {
        public static ITransport ThrottleTransport(this ITransport transport, int maxConcurrent)
        {
            foreach (var transportFactory in transport.TransportChannelFactories)
            {
                transportFactory.Middlewares.Add(new ThrottlingMiddleware(maxConcurrent));
            }

            return transport;
        }
        public static IHostConfiguration ThrottleHost(this IHostConfiguration configuration, int maxConcurrent)
        {
            configuration.Middlewares.Add(new ThrottlingMiddleware(maxConcurrent));
            return configuration;
        }
    }
}