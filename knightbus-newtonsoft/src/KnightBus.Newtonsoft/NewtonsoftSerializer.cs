using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using KnightBus.Messages;
using Newtonsoft.Json;

namespace KnightBus.Newtonsoft
{
    public class NewtonsoftSerializer : IMessageSerializer
    {
        public byte[] Serialize<T>(T message)
        {
            var serialized = JsonConvert.SerializeObject(message);
            return Encoding.UTF8.GetBytes(serialized);
        }

        public T Deserialize<T>(ReadOnlySpan<byte> serialized)
        {
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(serialized));
        }

        public T Deserialize<T>(ReadOnlyMemory<byte> serialized)
        {
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(serialized.Span));
        }

        public Task<T> Deserialize<T>(Stream serialized)
        {
            var serializer = new JsonSerializer();

            using var sr = new StreamReader(serialized);
            using var jsonTextReader = new JsonTextReader(sr);
            return Task.FromResult(serializer.Deserialize<T>(jsonTextReader));
        }

        public string ContentType => "application/json";
    }
}
