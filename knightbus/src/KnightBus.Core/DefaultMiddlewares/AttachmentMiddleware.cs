using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.DefaultMiddlewares
{
    public class AttachmentMiddleware : IMessageProcessorMiddleware
    {
        private readonly IMessageAttachmentProvider _attachmentProvider;

        public AttachmentMiddleware(IMessageAttachmentProvider attachmentProvider)
        {
            _attachmentProvider = attachmentProvider;
        }
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            IMessageAttachment attachment = null;
            var queueName = AutoMessageMapper.GetQueueName<T>();
            try
            {
                string attachmentId = null;
                if (typeof(ICommandWithAttachment).IsAssignableFrom(typeof(T)))
                {
                    attachmentId = AttachmentUtility.GetAttachmentIds(messageStateHandler.MessageProperties).FirstOrDefault();
                    if (!string.IsNullOrEmpty(attachmentId))
                    {
                        attachment = await _attachmentProvider.GetAttachmentAsync(queueName, attachmentId, cancellationToken).ConfigureAwait(false);
                        var message = (ICommandWithAttachment)messageStateHandler.GetMessage();
                        message.Attachment = attachment;
                    }
                }

                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
                if (attachment != null)
                {
                    attachment.Stream?.Dispose();
                    await _attachmentProvider.DeleteAttachmentAsync(queueName, attachmentId, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                attachment?.Stream?.Dispose();
            }
        }
    }
}
