using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Cosmos;

public static class CosmosExtensions
{
    public static IServiceCollection UseCosmos(
        this IServiceCollection collection, 
        Action<ICosmosConfiguration>? config = null)
    {
        var configuration = new CosmosConfiguration();
        config?.Invoke(configuration);
        collection.AddSingleton<ICosmosConfiguration>(_ => configuration);
        collection.AddScoped<ICosmosBus, CosmosBus>();
        collection.AddScoped<CosmosBus>();
        
        collection.AddSingleton<CosmosClient>(_ = new CosmosClient(configuration.ConnectionString, new CosmosClientOptions()
        {
            AllowBulkExecution = true,
            MaxRetryAttemptsOnRateLimitedRequests = 200,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60)
        }));
        
        return collection;
    }
}
