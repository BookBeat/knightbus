using KnightBus.Core;

namespace KnightBus.Redis
{
    public interface IRedisBusConfiguration : ITransportConfiguration
    {
        int DatabaseId { get; set; }
    }
    
    public class RedisConfiguration : IRedisBusConfiguration
    {
        public RedisConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public string ConnectionString { get; }
        public IMessageSerializer MessageSerializer { get; set; } = new JsonMessageSerializer();
        public int DatabaseId { get; set; }
    }
}