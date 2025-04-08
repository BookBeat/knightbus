using System.Collections.Generic;
using KnightBus.Messages;

namespace KnightBus.Redis.Management;

internal class DictionaryMessage : Dictionary<string, object>, IMessage { }
