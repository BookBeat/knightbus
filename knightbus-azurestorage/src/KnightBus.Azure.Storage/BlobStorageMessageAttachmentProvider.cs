using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage;

public class BlobStorageMessageAttachmentProvider : IMessageAttachmentProvider
{
    internal const string FileNameKey = "Filename";
    internal const string CompressionKey = "kb_compression";
    internal const string CompressionValueGzip = "gzip";
    private static readonly HashSet<string> Keys = [FileNameKey, CompressionKey];
    private readonly IStorageBusConfiguration _configuration;
    private readonly BlobStorageAttachmentOptions _options;

    public BlobStorageMessageAttachmentProvider(string connectionString)
        : this(new StorageBusConfiguration(connectionString)) { }

    public BlobStorageMessageAttachmentProvider(IStorageBusConfiguration configuration)
        : this(configuration, new BlobStorageAttachmentOptions()) { }

    public BlobStorageMessageAttachmentProvider(
        IStorageBusConfiguration configuration,
        BlobStorageAttachmentOptions options
    )
    {
        _configuration = configuration;
        _options = options ?? new BlobStorageAttachmentOptions();
    }

    public async Task<IMessageAttachment> GetAttachmentAsync(
        string queueName,
        string id,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var blob = AzureStorageClientFactory
            .CreateBlobContainerClient(_configuration, queueName)
            .GetBlobClient(id);
        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var blobStream = await blob.OpenReadAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var isCompressed =
            properties.Value.Metadata.TryGetValue(CompressionKey, out var compressionValue)
            && compressionValue == CompressionValueGzip;

        Stream resultStream;
        if (isCompressed)
        {
            resultStream = await DecompressStreamAsync(blobStream).ConfigureAwait(false);
        }
        else
        {
            resultStream = blobStream;
        }

        return new MessageAttachment(
            properties.Value.Metadata[FileNameKey],
            properties.Value.ContentType,
            resultStream,
            properties.Value.Metadata.ToDictionary(
                x => x.Key,
                x => Keys.Contains(x.Key) ? x.Value : FromBase64(x.Value)
            )
        );
    }

    public async Task UploadAttachmentAsync(
        string queueName,
        string id,
        IMessageAttachment attachment,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var requiredMetadata = new Dictionary<string, string>
        {
            { FileNameKey, attachment.Filename },
        };
        var userMetadata = attachment.Metadata.ToDictionary(x => x.Key, x => ToBase64(x.Value));

        var metadata = new Dictionary<string, string>(userMetadata);
        requiredMetadata.ToList().ForEach(x => metadata[x.Key] = x.Value); // Merge the dictionaries, on collisions, override keys in user's metadata with requiredMetadata

        Stream uploadStream = attachment.Stream;
        if (_options.EnableCompression)
        {
            uploadStream = await CompressStreamAsync(attachment.Stream, _options.CompressionLevel)
                .ConfigureAwait(false);
            metadata[CompressionKey] = CompressionValueGzip;
        }

        await UploadBlobAsync(
                queueName,
                id,
                uploadStream,
                attachment.ContentType,
                metadata,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task UploadBlobAsync(
        string queueName,
        string id,
        Stream uploadStream,
        string contentType,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken
    )
    {
        var blob = AzureStorageClientFactory
            .CreateBlobContainerClient(_configuration, queueName)
            .GetBlobClient(id);

        try
        {
            await blob.UploadAsync(
                    uploadStream,
                    new BlobHttpHeaders { ContentType = contentType },
                    metadata,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            var container = AzureStorageClientFactory.CreateBlobContainerClient(
                _configuration,
                queueName
            );
            try
            {
                await container
                    .CreateAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RequestFailedException ee) when (ee.Status == 409)
            {
                //Already created
            }

            // Reset stream position for retry
            uploadStream.Position = 0;
            await UploadBlobAsync(
                    queueName,
                    id,
                    uploadStream,
                    contentType,
                    metadata,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteAttachmentAsync(
        string queueName,
        string id,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var blob = AzureStorageClientFactory
            .CreateBlobContainerClient(_configuration, queueName)
            .GetBlobClient(id);
        try
        {
            await blob.DeleteAsync(DeleteSnapshotsOption.None, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ToBase64(string str) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(str));

    private static string FromBase64(string str) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(str));

    private static async Task<MemoryStream> CompressStreamAsync(
        Stream source,
        CompressionLevel compressionLevel
    )
    {
        var compressedStream = new MemoryStream();
        await using (
            var gzipStream = new GZipStream(compressedStream, compressionLevel, leaveOpen: true)
        )
        {
            await source.CopyToAsync(gzipStream).ConfigureAwait(false);
        }
        compressedStream.Position = 0;
        return compressedStream;
    }

    private static async Task<MemoryStream> DecompressStreamAsync(Stream source)
    {
        var decompressedStream = new MemoryStream();
        await using (var gzipStream = new GZipStream(source, CompressionMode.Decompress))
        {
            await gzipStream.CopyToAsync(decompressedStream).ConfigureAwait(false);
        }
        decompressedStream.Position = 0;
        return decompressedStream;
    }
}
