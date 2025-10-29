using System;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;

namespace KnightBus.Azure.Storage;

#nullable enable

public interface IStorageBusConfiguration : ITransportConfiguration
{
    /// <summary>
    /// Specifies if the bus should base64 encode or leave it up to the client. Base64 is mandatory for legacy.
    /// </summary>
    QueueMessageEncoding MessageEncoding { get; }

    /// <summary>
    /// If using managed identity: Name of the storage account to connect to. Used together with <see cref="Credential"/> to connect without a secret
    /// </summary>
    string? StorageAccountName { get; set; }

    /// <summary>
    /// If using managed identity: The Azure managed identity credential to use for authorization.
    /// </summary>
    TokenCredential? Credential { get; set; }
}

public class StorageBusConfiguration : IStorageBusConfiguration
{
    public StorageBusConfiguration(string connectionString)
        : this()
    {
        ConnectionString = connectionString;
    }

    public StorageBusConfiguration(string storageAccountName, TokenCredential credential)
        : this()
    {
        StorageAccountName = storageAccountName;
        Credential = credential;
    }

    public StorageBusConfiguration() { }

    /// <summary>
    /// <remarks>Should be left blank if using managed identity</remarks>
    /// </summary>
    public string ConnectionString { get; set; }
    public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();

    /// <summary>
    /// Specifies if the bus should base64 encode or leave it up to the client. Base64 is mandatory for legacy.
    /// </summary>
    public QueueMessageEncoding MessageEncoding { get; set; } = QueueMessageEncoding.Base64;

    public string? StorageAccountName { get; set; }
    public TokenCredential? Credential { get; set; }
}

public static class AzureStorageClientFactory
{
    public static QueueClient CreateQueueClient(
        IStorageBusConfiguration configuration,
        string queueName
    )
    {
        // QueueMessageEncoding.Base64 required for backwards compability with v11 storage clients

        if (!string.IsNullOrWhiteSpace(configuration.ConnectionString))
        {
            return new QueueClient(
                configuration.ConnectionString,
                queueName,
                new QueueClientOptions { MessageEncoding = configuration.MessageEncoding }
            );
        }

        if (
            !string.IsNullOrWhiteSpace(configuration.StorageAccountName)
            && configuration.Credential is not null
        )
        {
            return new QueueClient(
                new Uri(
                    $"https://{configuration.StorageAccountName}.queue.core.windows.net/{queueName}"
                ),
                configuration.Credential,
                new QueueClientOptions { MessageEncoding = configuration.MessageEncoding }
            );
        }

        throw new InvalidOperationException(
            $"{nameof(StorageBusConfiguration)} requires either a {nameof(configuration.ConnectionString)} or a {nameof(configuration.StorageAccountName)} with a {nameof(configuration.Credential)}."
        );
    }

    public static QueueServiceClient CreateQueueServiceClient(
        IStorageBusConfiguration configuration
    )
    {
        // QueueMessageEncoding.Base64 required for backwards compability with v11 storage clients

        if (!string.IsNullOrWhiteSpace(configuration.ConnectionString))
        {
            return new QueueServiceClient(configuration.ConnectionString);
        }

        if (
            !string.IsNullOrWhiteSpace(configuration.StorageAccountName)
            && configuration.Credential is not null
        )
        {
            return new QueueServiceClient(
                new Uri($"https://{configuration.StorageAccountName}.queue.core.windows.net/"),
                configuration.Credential
            );
        }

        throw new InvalidOperationException(
            $"{nameof(StorageBusConfiguration)} requires either a {nameof(configuration.ConnectionString)} or a {nameof(configuration.StorageAccountName)} with a {nameof(configuration.Credential)}."
        );
    }

    public static BlobContainerClient CreateBlobContainerClient(
        IStorageBusConfiguration configuration,
        string blobContainerName
    )
    {
        if (!string.IsNullOrWhiteSpace(configuration.ConnectionString))
        {
            return new BlobContainerClient(configuration.ConnectionString, blobContainerName);
        }

        if (
            !string.IsNullOrWhiteSpace(configuration.StorageAccountName)
            && configuration.Credential is not null
        )
        {
            return new BlobContainerClient(
                new Uri(
                    $"https://{configuration.StorageAccountName}.blob.core.windows.net/{blobContainerName}"
                ),
                configuration.Credential
            );
        }

        throw new InvalidOperationException(
            $"{nameof(StorageBusConfiguration)} requires either a {nameof(configuration.ConnectionString)} or a {nameof(configuration.StorageAccountName)} with a {nameof(configuration.Credential)}."
        );
    }
}
