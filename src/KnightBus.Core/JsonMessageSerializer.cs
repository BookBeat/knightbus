using System.Reflection;
using KnightBus.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace KnightBus.Core
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        public JsonSerializerSettings Settings { get; set; }

        public JsonMessageSerializer()
        {
            Settings = new JsonSerializerSettings { ContractResolver = new IgnoreAttachmentsResolver() };
        }
        public string Serialize<T>(T message)
        {
            return JsonConvert.SerializeObject(message, Settings);
        }


        public T Deserialize<T>(string serializedString)
        {
            return JsonConvert.DeserializeObject<T>(serializedString, Settings);
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