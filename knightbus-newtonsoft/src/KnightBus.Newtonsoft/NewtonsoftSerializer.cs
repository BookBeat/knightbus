using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using KnightBus.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace KnightBus.Newtonsoft
{
    public class NewtonsoftSerializer : IMessageSerializer
    {
        private JsonSerializerSettings _settings;

        public NewtonsoftSerializer()
        {
            _settings = new JsonSerializerSettings { ContractResolver = new IgnoreAttachmentsResolver() };
        }

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

    public class IgnoreAttachmentsResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);
            if (typeof(ICommandWithAttachment).IsAssignableFrom(member.DeclaringType) && member.Name == nameof(ICommandWithAttachment.Attachment))
            {
                prop.Ignored = true;
            }

            return prop;
        }
    }
}
