using System.Collections.Generic;

namespace KnightBus.Core
{
    public interface ITransportConfiguration
    {
        string ConnectionString { get; }
        IMessageSerializer MessageSerializer { get; set; }
        IMessageAttachmentProvider AttachmentProvider { get; set; }
        IList<IMessageProcessorMiddleware> Middlewares { get; }
    }
}