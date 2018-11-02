namespace KnightBus.Messages
{
    public interface ICommandWithAttachment
    {
        IMessageAttachment Attachment { get; set; }
    }
}