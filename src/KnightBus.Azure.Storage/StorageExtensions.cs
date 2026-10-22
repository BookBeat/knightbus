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
using Microsoft.Extensions.Logging;

namespace KnightBus.Azure.Storage;

public static class StorageExtensions
{
    public static IServiceCollection UseBlobStorageAttachments(this IServiceCollection services)
    {
        return services.UseBlobStorageAttachments(_ => { });
    }

    public static IServiceCollection UseBlobStorageAttachments(
        this IServiceCollection services,
        Action<BlobStorageAttachmentOptions> configureOptions
    )
    {
        var options = new BlobStorageAttachmentOptions();
        configureOptions(options);
        services.AddSingleton(options);
        services.AddSingleton<IMessageAttachmentProvider, BlobStorageMessageAttachmentProvider>();
        services.AddMiddleware<AttachmentMiddleware>();
        services.AddSingleton<IMessagePreProcessor, AttachmentPreProcessor>();
        return services;
    }

    public static IServiceCollection UseBlobStorage(
        this IServiceCollection services,
        Action<IStorageBusConfiguration>? config = null
    )
    {
        var storageConfig = new StorageBusConfiguration();
        config?.Invoke(storageConfig);
        services.AddSingleton<IStorageBusConfiguration>(storageConfig);
        services.AddScoped<IStorageBus, StorageBus>();
        return services;
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

    /// <summary>
    /// Uses blob leases as singleton locks. The locks are also spread over the hosts instead of
    /// letting the first host take them all, see <see cref="ISingletonWeight"/>. To keep the old
    /// behaviour, remove the <see cref="ISingletonMembership"/> registration.
    /// </summary>
    public static IServiceCollection UseBlobStorageLockManager(this IServiceCollection services)
    {
        services.AddSingleton<ISingletonLockManager>(provider =>
        {
            //Fails the host start, instead of every attempt to take a lock
            if (provider.GetService<SingletonOptions>() is { } options)
                BlobLockManager.EnsureLeasePeriod(options.LockDuration, nameof(options));
            return new BlobLockManager(
                provider.GetRequiredService<IStorageBusConfiguration>(),
                provider.GetService<IBlobLockScheme>()
            );
        });
        //The plain ILogger is not registered, so the logger is passed on explicitly
        services.AddSingleton<ISingletonMembership>(provider => new BlobSingletonMembership(
            provider.GetRequiredService<IStorageBusConfiguration>(),
            provider.GetService<IBlobLockScheme>(),
            provider.GetService<ILogger<BlobSingletonMembership>>()
        ));
        return services;
    }

    public static IServiceCollection UseBlobStorageSagas(this IServiceCollection services)
    {
        services.EnableSagas<BlobSagaStore>();
        return services;
    }
}
