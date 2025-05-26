using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.PreProcessors;

public class AttachmentPreProcessor : IMessagePreProcessor
{
    private readonly IMessageAttachmentProvider _messageAttachmentProvider;

    public AttachmentPreProcessor(IMessageAttachmentProvider messageAttachmentProvider)
    {
        _messageAttachmentProvider = messageAttachmentProvider;
    }

    public async Task<IDictionary<string, object>> PreProcess<T>(
        T message,
        CancellationToken cancellationToken
    )
        where T : IMessage
    {
        if (typeof(ICommandWithAttachment).IsAssignableFrom(typeof(T)))
        {
            var attachmentMessage = (ICommandWithAttachment)message;
            if (attachmentMessage.Attachment != null)
            {
                var attachmentIds = new List<string>();
                var attachmentId = Guid.NewGuid().ToString("N");
                await _messageAttachmentProvider
                    .UploadAttachmentAsync(
                        AutoMessageMapper.GetQueueName<T>(),
                        attachmentId,
                        attachmentMessage.Attachment,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                attachmentIds.Add(attachmentId);
                return new Dictionary<string, object>
                {
                    { AttachmentUtility.AttachmentKey, string.Join(",", attachmentIds) },
                };
            }
        }
        return new Dictionary<string, object>();
    }
}
