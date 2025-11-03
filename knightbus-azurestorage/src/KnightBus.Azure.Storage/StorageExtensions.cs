using System;
using Azure.Core;
using KnightBus.Azure.Storage.Sagas;
using KnightBus.Azure.Storage.Singleton;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Core.PreProcessors;
using KnightBus.Core.Sagas;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Azure.Storage;

public static class StorageExtensions
{
    public static IServiceCollection UseBlobStorageAttachments(this IServiceCollection services)
    {
        services.AddSingleton<IMessageAttachmentProvider, BlobStorageMessageAttachmentProvider>();
        services.AddMiddleware<AttachmentMiddleware>();
        services.AddSingleton<IMessagePreProcessor, AttachmentPreProcessor>();
        return services;
    }

    public static IServiceCollection UseBlobStorage(
        this IServiceCollection services,
        Func<IServiceProvider, IStorageBusConfiguration> configFactory
    )
    {
        services.AddSingleton<IStorageBusConfiguration>(configFactory.Invoke);
        services.AddScoped<IStorageBus, StorageBus>();
        return services;
    }

    public static IServiceCollection UseBlobStorage(
        this IServiceCollection services,
        Action<IStorageBusConfiguration> config = null
    )
    {
        var storageConfig = new StorageBusConfiguration();
        config?.Invoke(storageConfig);
        return services.UseBlobStorage(_ => storageConfig);
    }

    public static IServiceCollection UseBlobStorage(
        this IServiceCollection services,
        string connectionString
    )
    {
        return services.UseBlobStorage(configuration =>
        {
            configuration.ConnectionString = connectionString;
        });
    }

    public static IServiceCollection UseBlobStorage(
        this IServiceCollection services,
        string storageAccountName,
        TokenCredential credential
    )
    {
        return services.UseBlobStorage(configuration =>
        {
            configuration.StorageAccountName = storageAccountName;
            configuration.Credential = credential;
        });
    }

    public static IServiceCollection UseBlobStorageLockManager(
        this IServiceCollection services,
        IBlobLockScheme lockScheme
    )
    {
        services.AddSingleton(lockScheme);
        return services.UseBlobStorageLockManager();
    }

    public static IServiceCollection UseBlobStorageLockManager(this IServiceCollection services)
    {
        services.AddSingleton<ISingletonLockManager, BlobLockManager>();
        return services;
    }

    public static IServiceCollection UseBlobStorageSagas(this IServiceCollection services)
    {
        services.EnableSagas<BlobSagaStore>();
        return services;
    }
}
