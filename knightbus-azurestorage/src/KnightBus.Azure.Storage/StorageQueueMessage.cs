using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage
{
    public class StorageQueueMessage
    {
        public StorageQueueMessage(IMessage message)
        {
            Message = message;
        }
        public StorageQueueMessage()
        {
        }

        public string BlobMessageId
        {
            get => Properties.TryGetValue("_bmid", out var id) ? id : string.Empty;
            internal set => Properties["_bmid"] = value;
        }

        internal string QueueMessageId { get; set; }
        public string PopReciept { get; internal set; }
        public IMessage Message { get; internal set; }
        public int DequeueCount { get; set; }
        public Dictionary<string, string> Properties { get; internal set; } = new Dictionary<string, string>();

        public string[] GetAttachmentIds()
        {
            return AttachmentUtility.GetAttachmentIds(Properties);
        }
    }
}