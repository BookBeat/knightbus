using KnightBus.Core;
using KnightBus.Core.Sagas;

namespace KnightBus.SqlServer
{
    public static class SqlServerExtensions
    {
        public static IHostConfiguration UseSqlServerSagaStore(this IHostConfiguration configuration, string connectionString)
        {
            configuration.EnableSagas(new SqlServerSagaStore(connectionString));
            return configuration;
        }
    }
}