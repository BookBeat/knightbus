using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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

            return new MessageAttachment(properties.Value.Metadata["Filename"], properties.Value.ContentType, await blob.OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false));
        }

        public async Task UploadAttachmentAsync(string queueName, string id, IMessageAttachment attachment, CancellationToken cancellationToken = default(CancellationToken))
        {
            var blob = new BlobClient(_connectionString, queueName, id);
            try
            {
                await blob.UploadAsync(attachment.Stream, new BlobHttpHeaders { ContentType = attachment.ContentType }, new Dictionary<string, string> { { "Filename", attachment.Filename } }, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RequestFailedException e) when (e.Status == 404)
            {
                var container = new BlobContainerClient(_connectionString, queueName);
                try
                {
                    await container.CreateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (RequestFailedException ee) when (ee.Status == 409)
                {
                    //Already created
                }

                await UploadAttachmentAsync(queueName, id, attachment, cancellationToken).ConfigureAwait(false);
            }
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