using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

[assembly: InternalsVisibleTo("KnightBus.Redis")]
namespace KnightBus.Redis.Messages
{
    public class RedisDeadletter<T> where T : IRedisMessage
    {
        public string Id { get; set; }
        public T Body { get; set; }
        
        [IgnoreDataMember]
        public IDictionary<string, string> HashEntries { get; set; }
    }
}