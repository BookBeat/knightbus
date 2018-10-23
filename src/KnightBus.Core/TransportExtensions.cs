namespace KnightBus.Core
{
    public static class TransportExtensions
    {
        public static ITransport EnableAttachments(this ITransport transport, IMessageAttachmentProvider attachmentProvider)
        {
            transport.Configuration.AttachmentProvider = attachmentProvider;
            return transport;
        }

        public static ITransport SerializeMessagesWith(this ITransport transport, IMessageSerializer serializer)
        {
            transport.Configuration.MessageSerializer = serializer;
            return transport;
        }

        public static ITransport UseJsonMessageSerializer(this ITransport transport)
        {
            transport.Configuration.MessageSerializer = new JsonMessageSerializer();
            return transport;
        }
    }
}