using System;
using System.IO;
using System.Threading.Tasks;

namespace KnightBus.Messages
{
    /// <summary>
    /// Determines how messages are serialized when transported
    /// </summary>
    public interface IMessageSerializer
    {
        byte[] Serialize<T>(T message);
        T Deserialize<T>(ReadOnlySpan<byte> serialized);
        T Deserialize<T>(ReadOnlyMemory<byte> serialized);
        Task<T> Deserialize<T>(Stream serialized);
        string ContentType { get; }
    }
}