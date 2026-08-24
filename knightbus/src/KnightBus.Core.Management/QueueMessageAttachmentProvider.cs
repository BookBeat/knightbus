using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Management;

public class QueueMessageAttachmentProvider(IMessageAttachmentProvider attachmentProvider)
    : IQueueMessageAttachmentProvider
{
    public async Task<QueueMessageAttachment> GetAttachment(
        string queue,
        Dictionary<string, string> messageProperties,
        CancellationToken cancellationToken
    )
    {
        var attachmentId = AttachmentUtility.GetAttachmentIds(messageProperties).FirstOrDefault();
        if (string.IsNullOrEmpty(attachmentId))
            return null;

        var attachment = await attachmentProvider.GetAttachmentAsync(
            queue,
            attachmentId,
            cancellationToken
        );

        if (attachment is null)
            return null;

        return new QueueMessageAttachment(
            attachment.Stream,
            attachment.ContentType,
            attachment.Filename,
            attachment.Length
        );
    }

    public bool HasAttachment(Dictionary<string, string> messageProperties)
    {
        return !string.IsNullOrEmpty(
            AttachmentUtility.GetAttachmentIds(messageProperties).FirstOrDefault()
        );
    }
}
