using System.IO;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage
{
    internal class BlobMessageAttachment : IMessageAttachment
    {
        public string Filename { get; set; }
        public string ContentType { get; set; }
        public long Length { get; set; }
        public Stream Stream { get; set; }
    }
}