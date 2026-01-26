using System.IO.Compression;

namespace KnightBus.Azure.Storage;

public class BlobStorageAttachmentOptions
{
    public bool EnableCompression { get; set; } = false;
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
}
