using System.IO;

namespace KnightBus.Messages;

public interface IMessageAttachment
{
    string Filename { get; }
    string ContentType { get; }
    long Length { get; }
    Stream Stream { get; }
}
