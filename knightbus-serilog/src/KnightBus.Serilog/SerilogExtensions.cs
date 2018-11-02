using KnightBus.Core;
using Serilog;

namespace KnightBus.Serilog
{
    public static class SerilogExtensions
    {
        public static IHostConfiguration UseSerilog(this IHostConfiguration configuration, ILogger logger)
        {
            configuration.Log = new SerilogProvider(logger);
            return configuration;
        }
    }
}