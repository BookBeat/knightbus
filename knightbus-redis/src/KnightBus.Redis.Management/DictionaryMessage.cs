using System.Collections.Generic;
using KnightBus.Messages;
using KnightBus.Redis.Messages;

namespace KnightBus.Redis.Management;

internal class DictionaryMessage : Dictionary<string, object>, IMessage
{

}
