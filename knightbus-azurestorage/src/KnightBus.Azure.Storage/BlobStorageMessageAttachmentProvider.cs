using System;
using System.Collections.Generic;
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
    private static readonly HashSet<string> Keys = [FileNameKey];
    private readonly IStorageBusConfiguration _configuration;

    public BlobStorageMessageAttachmentProvider(string connectionString)
        : this(new StorageBusConfiguration(connectionString)) { }

    public BlobStorageMessageAttachmentProvider(IStorageBusConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<IMessageAttachment> GetAttachmentAsync(
        string queueName,
        string id,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var blob = NameMeClientFactory
            .CreateBlobContainerClient(_configuration, queueName)
            .GetBlobClient(id);
        var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new MessageAttachment(
            properties.Value.Metadata[FileNameKey],
            properties.Value.ContentType,
            await blob.OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false),
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
        var blob = NameMeClientFactory
            .CreateBlobContainerClient(_configuration, queueName)
            .GetBlobClient(id);

        try
        {
            var requiredMetadata = new Dictionary<string, string>
            {
                { FileNameKey, attachment.Filename },
            };
            var userMetadata = attachment.Metadata.ToDictionary(x => x.Key, x => ToBase64(x.Value));

            var metadata = new Dictionary<string, string>(userMetadata);
            requiredMetadata.ToList().ForEach(x => metadata[x.Key] = x.Value); // Merge the dictionaries, on collisions, override keys in user's metadata with requiredMetadata

            await blob.UploadAsync(
                    attachment.Stream,
                    new BlobHttpHeaders { ContentType = attachment.ContentType },
                    metadata,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            var container = NameMeClientFactory.CreateBlobContainerClient(
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

            await UploadAttachmentAsync(queueName, id, attachment, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<bool> DeleteAttachmentAsync(
        string queueName,
        string id,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var blob = NameMeClientFactory
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
}
