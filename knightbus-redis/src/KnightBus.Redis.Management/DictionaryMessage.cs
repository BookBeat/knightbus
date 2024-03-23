using System.Collections.Generic;
using KnightBus.Redis.Messages;

namespace KnightBus.Redis.Management;

internal class DictionaryMessage : Dictionary<string, object>, IRedisMessage
{

}
