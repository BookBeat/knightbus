using System;
using System.IO;
using System.Threading.Tasks;
using KnightBus.Messages;
using MessagePack;

namespace KnightBus.MessagePack
{
    public class MessagePackCSharpSerializer : IMessageSerializer
    {
        private readonly MessagePackSerializerOptions _options;

        public MessagePackCSharpSerializer(MessagePackSerializerOptions options = null)
        {
            _options = options;
        }

        public byte[] Serialize<T>(T message)
        {
            return MessagePackSerializer.Serialize(message, _options);
        }

        public T Deserialize<T>(ReadOnlySpan<byte> serialized)
        {
            return MessagePackSerializer.Deserialize<T>(serialized.ToArray(), _options);
        }

        public T Deserialize<T>(ReadOnlyMemory<byte> serialized)
        {
            return MessagePackSerializer.Deserialize<T>(serialized, _options);
        }

        public Task<T> Deserialize<T>(Stream serialized)
        {
            return Task.FromResult(MessagePackSerializer.Deserialize<T>(serialized, _options));
        }

        public string ContentType => "application/msgpack";
    }
}
