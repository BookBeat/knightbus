using KnightBus.Core.Sagas;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.SqlServer;

public static class SqlServerExtensions
{
    public static IServiceCollection UseSqlServerSagaStore(
        this IServiceCollection services,
        string connectionString
    )
    {
        services.EnableSagas(new SqlServerSagaStore(connectionString));
        return services;
    }
}
