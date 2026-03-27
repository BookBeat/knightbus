using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage;

public class BlobStorageMessageAttachmentProvider : IMessageAttachmentProvider
{
    internal const string FileNameKey = "Filename";
    private static readonly HashSet<string> Keys = [FileNameKey];
    private readonly IStorageBusConfiguration _configuration;
    private readonly BlobStorageAttachmentOptions _options;

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
        Stream uploadStream = attachment.Stream;
        if (_options.EnableCompression)
        {
            uploadStream = CreateCompressingStream(attachment.Stream, _options.CompressionLevel);
            id = $"{id}{CompressedFileExtension}";
            contentEncoding = "br";
        }

        var blobHttpHeaders = new BlobHttpHeaders
        {
            ContentType = attachment.ContentType,
            ContentEncoding = contentEncoding,
        };

        await UploadBlobAsync(
                queueName,
                id,
                uploadStream,
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
        await container
            .CreateIfNotExistsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var blob = container.GetBlobClient(id);
        await blob.UploadAsync(
                uploadStream,
                blobHttpHeaders,
                metadata,
                cancellationToken: cancellationToken
            )
            .ConfigureAwait(false);
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

    private static Stream CreateCompressingStream(Stream source, CompressionLevel compressionLevel)
    {
        var pipe = new Pipe();
        _ = Task.Run(async () =>
        {
            try
            {
                await using var brotliStream = new BrotliStream(
                    pipe.Writer.AsStream(),
                    compressionLevel,
                    leaveOpen: true
                );
                await source.CopyToAsync(brotliStream).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await pipe.Writer.CompleteAsync(ex).ConfigureAwait(false);
                return;
            }
            await pipe.Writer.CompleteAsync().ConfigureAwait(false);
        });
        return pipe.Reader.AsStream();
    }
}
