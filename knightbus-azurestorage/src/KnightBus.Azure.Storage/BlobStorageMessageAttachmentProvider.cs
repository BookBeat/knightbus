using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage
{
    public class BlobStorageMessageAttachmentProvider : IMessageAttachmentProvider
    {
        private readonly string _connectionString;

        public BlobStorageMessageAttachmentProvider(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IMessageAttachment> GetAttachmentAsync(string queueName, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            var blob = new BlobClient(_connectionString, queueName, id);
            var properties = await blob.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            
            return new BlobMessageAttachment
            {
                Stream = await blob.OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false),
                Length = properties.Value.ContentLength,
                Filename = properties.Value.Metadata["Filename"],
                ContentType = properties.Value.ContentType
            };
        }

        public async Task UploadAttachmentAsync(string queueName, string id, IMessageAttachment attachment, CancellationToken cancellationToken = default(CancellationToken))
        {
            var blob = new BlobClient(_connectionString, queueName, id);
            await blob.UploadAsync(attachment.Stream, new BlobHttpHeaders {ContentType = attachment.ContentType}, new Dictionary<string, string> {{"Filename", attachment.Filename}}, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<bool> DeleteAttachmentAsync(string queueName, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            var blob = new BlobClient(_connectionString, queueName, id);
            try
            {
                await blob.DeleteAsync(DeleteSnapshotsOption.None, cancellationToken: cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}