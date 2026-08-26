using KnightBus.Messages;

namespace KnightBus.Core.Tests.Unit;

public class AttachmentCommand : ICommandWithAttachment, ICommand
{
    public string Message { get; set; } = null!;
    public string MessageId { get; set; } = null!;
    public IMessageAttachment? Attachment { get; set; }
}

public class AttachmentCommandMapping : IMessageMapping<AttachmentCommand>
{
    public string QueueName => "attachment-queue";
}
