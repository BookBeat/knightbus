using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;

namespace KnightBus.Core.DefaultMiddlewares;

/// <summary>
/// Loads the attachment for <see cref="ICommandWithAttachment"/> messages before processing and
/// deletes it after the message has been successfully processed.
/// The attachment is kept when processing fails, since a retry needs it, and when the message is
/// dead lettered, so the message can be inspected or requeued with its attachment intact.
/// Attachments of dead lettered messages are never cleaned up by KnightBus; use a lifecycle
/// policy in the attachment store when they must expire.
/// </summary>
public class AttachmentMiddleware : IMessageProcessorMiddleware
{
    private readonly IMessageAttachmentProvider _attachmentProvider;

    public AttachmentMiddleware(IMessageAttachmentProvider attachmentProvider)
    {
        _attachmentProvider = attachmentProvider;
    }

    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        IMessageAttachment? attachment = null;
        var queueName = AutoMessageMapper.GetQueueName<T>();
        try
        {
            string? attachmentId = null;
            if (typeof(ICommandWithAttachment).IsAssignableFrom(typeof(T)))
            {
                attachmentId = AttachmentUtility
                    .GetAttachmentIds(messageStateHandler.MessageProperties)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(attachmentId))
                {
                    attachment = await _attachmentProvider
                        .GetAttachmentAsync(queueName, attachmentId, cancellationToken)
                        .ConfigureAwait(false);
                    var message = (ICommandWithAttachment)messageStateHandler.GetMessage();
                    message.Attachment = attachment;
                }
            }

            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            if (attachment != null)
            {
                try
                {
                    await _attachmentProvider
                        .DeleteAttachmentAsync(queueName, attachmentId!, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    //The message is already completed when the delete runs, so throwing here
                    //would only trigger a failed abandon of a completed message. The attachment
                    //is orphaned and must be cleaned up by the attachment store
                    pipelineInformation?.HostConfiguration?.Log?.LogWarning(
                        e,
                        "Failed to delete attachment {AttachmentId} for {QueueName}",
                        attachmentId,
                        queueName
                    );
                }
            }
        }
        finally
        {
            attachment?.Stream?.Dispose();
        }
    }
}
