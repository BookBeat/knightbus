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
    private readonly IStorageBusConfiguration _configuration;
    private readonly BlobStorageAttachmentOptions _options;

    private readonly ConcurrentDictionary<string, byte> _knownContainers = new();

    private const string CompressedFileExtension = ".brotli";
    private const long SingleRequestThresholdBytes = 4 * 1024 * 1024;

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

        var metadata = properties.Value.Metadata.ToDictionary(
            x => x.Key,
            x =>
                x.Key == FileNameKey || (isCompressed && x.Key == UncompressedLengthKey)
                    ? x.Value
                    : FromBase64(x.Value)
        );

        // A decompressing stream cannot report its length, so it is kept in blob
        // metadata. Removed here so the metadata a handler sees does not depend on
        // whether compression is enabled
        var length = properties.Value.ContentLength;
        if (isCompressed)
        {
            length =
                metadata.Remove(UncompressedLengthKey, out var stored)
                && TryReadUncompressedLength(stored, out var uncompressedLength)
                    ? uncompressedLength
                    : 0;
        }

        return new BlobMessageAttachment(
            properties.Value.Metadata[FileNameKey],
            properties.Value.ContentType,
            resultStream,
            metadata,
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
            // The bytes that will actually be uploaded - attachment.Length is the whole
            // stream even when positioned mid-stream, and 0 when non-seekable, where the
            // key is omitted and readers fall back to 0. Base64 like every other value
            // so readers predating this key can still decode it
            if (attachment.Stream.CanSeek)
            {
                metadata[UncompressedLengthKey] = ToBase64(
                    (attachment.Stream.Length - attachment.Stream.Position).ToString(
                        CultureInfo.InvariantCulture
                    )
                );
            }
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

        var startPosition = uploadStream.CanSeek ? uploadStream.Position : 0;

        try
        {
            await UploadCoreAsync().ConfigureAwait(false);
        }
        catch (RequestFailedException e) when (e.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            // The container was deleted after its existence was cached. Recreate it
            // and retry once, unless the source cannot be replayed
            _knownContainers.TryRemove(queueName, out _);
            if (!uploadStream.CanSeek)
            {
                throw;
            }

            await container
                .CreateIfNotExistsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            _knownContainers.TryAdd(queueName, 0);

            if (uploadStream.CanSeek)
            {
                uploadStream.Position = startPosition;
            }
            await UploadCoreAsync().ConfigureAwait(false);
        }

        async Task UploadCoreAsync()
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
    }

    private async Task UploadCompressedBlobAsync(
        BlockBlobClient blob,
        Stream uploadStream,
        BlobHttpHeaders blobHttpHeaders,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken
    )
    {
        Stream blobStream = null;
        try
        {
            // Compress into memory first: a result within the threshold uploads as a
            // single request. Only when it outgrows the threshold is the blob write
            // stream opened and the buffer drained into it as staged blocks. On failure
            // the streams are deliberately not disposed: disposing the blob write stream
            // would commit the partially staged blocks as a truncated blob, while
            // uncommitted blocks are garbage collected by the service
            var buffer = new MemoryStream();
            var compressionStream = new BrotliStream(
                buffer,
                _options.CompressionLevel,
                leaveOpen: true
            );
            var chunk = new byte[81920];
            int read;
            while (
                (
                    read = await uploadStream
                        .ReadAsync(chunk, cancellationToken)
                        .ConfigureAwait(false)
                ) > 0
            )
            {
                await compressionStream
                    .WriteAsync(chunk.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
                if (blobStream == null && buffer.Length <= SingleRequestThresholdBytes)
                {
                    continue;
                }

                blobStream ??= await blob.OpenWriteAsync(
                        overwrite: true,
                        options: new BlockBlobOpenWriteOptions
                        {
                            HttpHeaders = blobHttpHeaders,
                            Metadata = metadata,
                        },
                        cancellationToken: cancellationToken
                    )
                    .ConfigureAwait(false);
                buffer.Position = 0;
                await buffer.CopyToAsync(blobStream, cancellationToken).ConfigureAwait(false);
                buffer.SetLength(0);
            }
            await compressionStream.DisposeAsync().ConfigureAwait(false);
            buffer.Position = 0;

            if (blobStream == null)
            {
                await blob.UploadAsync(
                        buffer,
                        new BlobUploadOptions
                        {
                            HttpHeaders = blobHttpHeaders,
                            Metadata = metadata,
                        },
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            else
            {
                await buffer.CopyToAsync(blobStream, cancellationToken).ConfigureAwait(false);
                await blobStream.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            if (blobStream != null)
            {
                // OpenWrite creates the blob before any data is written, so clean up the
                // empty one it leaves behind. The caller's token is usually already
                // cancelled at this point, hence None
                try
                {
                    await blob.DeleteIfExistsAsync(cancellationToken: CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Best effort, never mask the original failure
                }
            }
            throw;
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

    private static bool TryReadUncompressedLength(string stored, out long length)
    {
        length = 0;
        try
        {
            return long.TryParse(
                FromBase64(stored),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out length
            );
        }
        catch (FormatException)
        {
            return false;
        }
    }

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
