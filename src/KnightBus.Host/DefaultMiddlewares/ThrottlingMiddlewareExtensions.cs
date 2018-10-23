using KnightBus.Core;

namespace KnightBus.Host.DefaultMiddlewares
{
    public static class ThrottlingMiddlewareExtensions
    {
        public static ITransport ThrottleTransport(this ITransport configuration, int maxConcurrent)
        {
            configuration.Configuration.Middlewares.Add(new ThrottlingMiddleware(maxConcurrent));
            return configuration;
        }
        public static IHostConfiguration ThrottleHost(this IHostConfiguration configuration, int maxConcurrent)
        {
            configuration.Middlewares.Add(new ThrottlingMiddleware(maxConcurrent));
            return configuration;
        }
    }
}