using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core
{
    internal class AttachmentTypeMapping: JsonConverter<IMessageAttachment>
    {
        public override IMessageAttachment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
           return null;
        }

        public override void Write(Utf8JsonWriter writer, IMessageAttachment value, JsonSerializerOptions options)
        {
            writer.WriteNullValue();
        }
    }

    public class MicrosoftJsonSerializer : IMessageSerializer
    {
        private readonly JsonSerializerOptions _options;

        public MicrosoftJsonSerializer(JsonSerializerOptions? options = null)
        {
            _options = options ?? new JsonSerializerOptions();
            _options.Converters.Add(new AttachmentTypeMapping());
        }
        public byte[] Serialize<T>(T message)
        {
            var s = JsonSerializer.Serialize(message, _options);
            return Encoding.UTF8.GetBytes(s);
        }

        public T Deserialize<T>(ReadOnlySpan<byte> serialized)
        {
            return JsonSerializer.Deserialize<T>(serialized, _options);
        }
        
        public Task<T>  Deserialize<T>(Stream serialized)
        {
            return JsonSerializer.DeserializeAsync<T>(serialized, _options).AsTask();
        }

        public string ContentType => "application/json";
    }
}