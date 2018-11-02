using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace KnightBus.Azure.Storage
{
    public class BlobStorageMessageAttachmentProvider : IMessageAttachmentProvider
    {
        private readonly CloudBlobClient _blobClient;

        public BlobStorageMessageAttachmentProvider(string connectionString)
        {
            var storage = CloudStorageAccount.Parse(connectionString);
            _blobClient = storage.CreateCloudBlobClient();
        }

        public async Task<IMessageAttachment> GetAttachmentAsync(string queueName, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            var container = _blobClient.GetContainerReference(queueName);
            var blob = container.GetBlockBlobReference(id);
            await blob.FetchAttributesAsync().ConfigureAwait(false);

            return new BlobMessageAttachment
            {
                Stream = await blob.OpenReadAsync(null, null, null, cancellationToken).ConfigureAwait(false),
                Length = blob.Properties.Length,
                Filename = blob.Metadata["Filename"],
                ContentType = blob.Properties.ContentType
            };
        }

        public async Task UploadAttachmentAsync(string queueName, string id, IMessageAttachment attachment, CancellationToken cancellationToken = default(CancellationToken))
        {
            var container = _blobClient.GetContainerReference(queueName);
            var blob = container.GetBlockBlobReference(id);
            blob.Properties.ContentType = attachment.ContentType;
            blob.Metadata.Add("Filename", attachment.Filename);
            await blob.UploadFromStreamAsync(attachment.Stream, null, null, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> DeleteAttachmentAsync(string queueName, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            var container = _blobClient.GetContainerReference(queueName);
            var blob = container.GetBlockBlobReference(id);
            try
            {
                await blob.DeleteAsync(DeleteSnapshotsOption.None, null, null, null, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}