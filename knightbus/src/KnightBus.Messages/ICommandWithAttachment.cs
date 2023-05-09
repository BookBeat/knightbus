namespace KnightBus.Messages
{
    /// <summary>
    /// Enables <see cref="ICommand"/> to have large attachments.
    /// Both sender and receiver must register an implementation for transporting attachments.
    /// </summary>
    public interface ICommandWithAttachment
    {
        IMessageAttachment Attachment { get; set; }
    }
}
