using System;
using System.IO;
using System.Text.Json.Serialization;
using KnightBus.Messages;

namespace KnightBus.Core
{
    public class MessageAttachment : IMessageAttachment
    {
        public MessageAttachment(string filename, string contentType, Stream stream)
        {
            Filename = filename;
            ContentType = contentType;
            Stream = stream;
            try
            {
                Length = stream.Length;
            }
            catch (NotSupportedException)
            {}
            
        }
        public string Filename { get; protected set; }
        public string ContentType { get; protected set; }
        public long Length { get; protected set; }
        [JsonIgnore]
        public Stream Stream { get; protected set; }
    }
}