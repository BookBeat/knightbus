using Newtonsoft.Json;

//This file should definitely be moved somewhere else eventually

namespace KnightBus.Examples.CosmosDB
{
    public class Message
    {
        [JsonProperty(PropertyName = "id")]
        public string id { get; set; }
        public string Topic { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

}
