using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;

namespace KnightBus.Redis
{
    public interface IRedisConfiguration : ITransportConfiguration
    {
        int DatabaseId { get; set; }
    }
    
    public class RedisConfiguration : IRedisConfiguration
    {
        public RedisConfiguration()
        {
        }
        public RedisConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public string ConnectionString { get; set; }
        public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
        public int DatabaseId { get; set; }
    }
}