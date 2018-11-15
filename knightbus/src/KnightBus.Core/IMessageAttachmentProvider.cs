using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core
{
    /// <summary>
    /// File attachment provider
    /// </summary>
    public interface IMessageAttachmentProvider
    {
        Task<IMessageAttachment> GetAttachmentAsync(string queueName, string id, CancellationToken cancellationToken = default(CancellationToken));
        Task UploadAttachmentAsync(string queueName, string id, IMessageAttachment attachment, CancellationToken cancellationToken = default(CancellationToken));
        Task<bool> DeleteAttachmentAsync(string queueName, string id, CancellationToken cancellationToken = default(CancellationToken));
    }
}