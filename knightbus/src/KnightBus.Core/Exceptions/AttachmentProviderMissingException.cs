namespace KnightBus.Core.Exceptions
{
    public sealed class AttachmentProviderMissingException : KnightBusException
    {
        public AttachmentProviderMissingException():base("No IAttachmentProvider found, did you forget to configure it?")
        {}
    }
}