using System;
using System.IO;
using System.Threading.Tasks;
using KnightBus.Core;
using ProtoBuf;

namespace KnightBus.ProtoBufNet
{
    public class ProtobufNetSerializer : IMessageSerializer
    {
        public byte[] Serialize<T>(T message)
        {
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, message);
            return stream.ToArray();
        }

        public T Deserialize<T>(ReadOnlySpan<byte> serialized)
        {
            return Serializer.Deserialize<T>(serialized);
        }

        public Task<T> Deserialize<T>(Stream serialized)
        {
            return Task.FromResult(Serializer.Deserialize<T>(serialized));
        }

        public string ContentType => "application/protobuf";
    }
}