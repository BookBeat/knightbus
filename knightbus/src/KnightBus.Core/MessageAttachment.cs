using System;
using System.Collections.Generic;
using System.IO;
using KnightBus.Messages;

namespace KnightBus.Core;

public class MessageAttachment : IMessageAttachment
{
    public MessageAttachment(string filename, string contentType, Stream stream, Dictionary<string,string> metadata = null)
    {
        Filename = filename;
        ContentType = contentType;
        Stream = stream;
        Metadata = metadata ?? [];
        try
        {
            Length = stream?.Length ?? 0;
        }
        catch (NotSupportedException)
        { }

    }
    public string Filename { get; protected set; }
    public string ContentType { get; protected set; }
    public long Length { get; protected set; }
    public Stream Stream { get; protected set; }
    public Dictionary<string, string> Metadata { get; protected set; }
}
