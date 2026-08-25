using System.IO;

namespace KnightBus.Core.Management;

public record QueueMessageAttachment(
    Stream Stream,
    string ContentType,
    string FileName,
    long Length
);
