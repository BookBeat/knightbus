using System.Text.Json;

namespace KnightBus.Core
{
    public class MicrosoftJsonSerializer : IMessageSerializer
    {
        private readonly JsonSerializerOptions _options;

        public MicrosoftJsonSerializer(JsonSerializerOptions options = null)
        {
            _options = options;
        }
        public string Serialize<T>(T message)
        {
            return JsonSerializer.Serialize(message, _options);
        }

        public T Deserialize<T>(string serializedString)
        {
            return JsonSerializer.Deserialize<T>(serializedString, _options);
        }

        public string ContentType => "application/json";
    }
}