using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage;

public class BlobStorageMessageAttachmentProvider : IMessageAttachmentProvider
{
    internal const string FileNameKey = "Filename";
    internal const string UncompressedLengthKey = "UncompressedLength";
    private static readonly HashSet<string> Keys = [FileNameKey, UncompressedLengthKey];
    private readonly IStorageBusConfiguration _configuration;
    private readonly BlobStorageAttachmentOptions _options;

    // Containers this instance has already created or confirmed exist, so the
    // existence check is paid once per container instead of on every upload.
    private readonly ConcurrentDictionary<string, byte> _knownContainers = new();

    private const string CompressedFileExtension = ".brotli";

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

        var isCompressed = blob.Name.EndsWith(CompressedFileExtension);

        Stream resultStream = isCompressed
            ? new BrotliStream(blobStream, CompressionMode.Decompress, leaveOpen: false)
            : blobStream;

        // A decompressing stream cannot report its length; the original size is
        // kept in blob metadata instead. Compressed blobs uploaded before the key
        // existed report 0, matching MessageAttachment's non-seekable behaviour.
        var length = properties.Value.ContentLength;
        if (isCompressed)
        {
            length =
                properties.Value.Metadata.TryGetValue(
                    UncompressedLengthKey,
                    out var uncompressedLength
                )
                && long.TryParse(
                    uncompressedLength,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsedLength
                )
                    ? parsedLength
                    : 0;
        }

        return new BlobMessageAttachment(
            properties.Value.Metadata[FileNameKey],
            properties.Value.ContentType,
            resultStream,
            properties.Value.Metadata.ToDictionary(
                x => x.Key,
                x => Keys.Contains(x.Key) ? x.Value : FromBase64(x.Value)
            ),
            length
        );
    }

    public async Task<string> UploadAttachmentAsync(
        string queueName,
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

        var id = Guid.NewGuid().ToString("N");
        string contentEncoding = null;
        if (_options.EnableCompression)
        {
            id = $"{id}{CompressedFileExtension}";
            contentEncoding = "br";
            metadata[UncompressedLengthKey] = attachment.Length.ToString(
                CultureInfo.InvariantCulture
            );
        }

        var blobHttpHeaders = new BlobHttpHeaders
        {
            ContentType = attachment.ContentType,
            ContentEncoding = contentEncoding,
        };

        await UploadBlobAsync(
                queueName,
                id,
                attachment.Stream,
                blobHttpHeaders,
                metadata,
                cancellationToken
            )
            .ConfigureAwait(false);

        return id;
    }

    private async Task UploadBlobAsync(
        string queueName,
        string id,
        Stream uploadStream,
        BlobHttpHeaders blobHttpHeaders,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken
    )
    {
        var container = AzureStorageClientFactory.CreateBlobContainerClient(
            _configuration,
            queueName
        );

        if (!_knownContainers.ContainsKey(queueName))
        {
            await container
                .CreateIfNotExistsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            _knownContainers.TryAdd(queueName, 0);
        }

        try
        {
            if (_options.EnableCompression)
            {
                await UploadCompressedBlobAsync(
                        container.GetBlockBlobClient(id),
                        uploadStream,
                        blobHttpHeaders,
                        metadata,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            else
            {
                await container
                    .GetBlobClient(id)
                    .UploadAsync(
                        uploadStream,
                        blobHttpHeaders,
                        metadata,
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }
        catch (RequestFailedException e) when (e.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            // The container was deleted after its existence was cached
            _knownContainers.TryRemove(queueName, out _);
            throw;
        }
    }

    private async Task UploadCompressedBlobAsync(
        BlockBlobClient blob,
        Stream uploadStream,
        BlobHttpHeaders blobHttpHeaders,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken
    )
    {
        var options = new BlockBlobOpenWriteOptions
        {
            HttpHeaders = blobHttpHeaders,
            Metadata = metadata,
        };

        // Compresses straight into the blob write stream, so neither the source
        // nor the compressed result is ever buffered in full. On failure the
        // streams are deliberately left undisposed: disposing the blob write
        // stream commits the partially staged block list as a truncated blob,
        // whereas uncommitted blocks are garbage collected by the service. A
        // failed upload can leave the zero-byte placeholder OpenWrite creates,
        // but its id is never returned so nothing references it.
        var blobStream = await blob.OpenWriteAsync(
                overwrite: true,
                options: options,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
        var compressionStream = new BrotliStream(
            blobStream,
            _options.CompressionLevel,
            leaveOpen: true
        );
        await uploadStream.CopyToAsync(compressionStream, cancellationToken).ConfigureAwait(false);
        // Flush the final compressed block, then commit the blob
        await compressionStream.DisposeAsync().ConfigureAwait(false);
        await blobStream.DisposeAsync().ConfigureAwait(false);
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

    private sealed class BlobMessageAttachment : MessageAttachment
    {
        public BlobMessageAttachment(
            string filename,
            string contentType,
            Stream stream,
            Dictionary<string, string> metadata,
            long length
        )
            : base(filename, contentType, stream, metadata)
        {
            Length = length;
        }
    }
}
