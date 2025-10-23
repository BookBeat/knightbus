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

    QueueClient CreateQueueClient(string queueName);
    BlobContainerClient CreateBlobContainerClient(string queueName);
    QueueServiceClient CreateQueueServiceClient();
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

    public QueueClient CreateQueueClient(string queueName)
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return new QueueClient(
                ConnectionString,
                queueName,
                new QueueClientOptions { MessageEncoding = MessageEncoding }
            );
        }

        if (!string.IsNullOrWhiteSpace(StorageAccountName) && Credential is not null)
        {
            return new QueueClient(
                new Uri($"https://{StorageAccountName}.queue.core.windows.net/{queueName}"),
                Credential,
                new QueueClientOptions { MessageEncoding = MessageEncoding }
            );
        }

        throw new InvalidOperationException(
            $"{nameof(StorageBusConfiguration)} requires either a {nameof(ConnectionString)} or a {nameof(StorageAccountName)} with a {nameof(Credential)}."
        );
    }

    public QueueServiceClient CreateQueueServiceClient()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return new QueueServiceClient(ConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(StorageAccountName) && Credential is not null)
        {
            return new QueueServiceClient(
                new Uri($"https://{StorageAccountName}.queue.core.windows.net/"),
                Credential
            );
        }

        throw new InvalidOperationException(
            $"{nameof(StorageBusConfiguration)} requires either a {nameof(ConnectionString)} or a {nameof(StorageAccountName)} with a {nameof(Credential)}."
        );
    }

    public BlobContainerClient CreateBlobContainerClient(string blobContainerName)
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return new BlobContainerClient(ConnectionString, blobContainerName);
        }

        if (!string.IsNullOrWhiteSpace(StorageAccountName) && Credential is not null)
        {
            return new BlobContainerClient(
                new Uri($"https://{StorageAccountName}.blob.core.windows.net/{blobContainerName}"),
                Credential
            );
        }

        throw new InvalidOperationException(
            $"{nameof(StorageBusConfiguration)} requires either a {nameof(ConnectionString)} or a {nameof(StorageAccountName)} with a {nameof(Credential)}."
        );
    }
}
