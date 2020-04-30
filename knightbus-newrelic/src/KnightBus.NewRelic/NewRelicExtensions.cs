using KnightBus.Core;

namespace KnightBus.NewRelicMiddleware
{
    public static class NewRelicExtensions
    {
        public static IHostConfiguration UseNewRelic(this IHostConfiguration configuration)
        {
            configuration.AddMiddleware(new NewRelicMessageMiddleware());
            return configuration;
        }
    }
}