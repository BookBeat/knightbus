using System.Collections.Generic;
using KnightBus.Azure.Storage.Messages;

namespace KnightBus.Azure.Storage.Management;

internal class DictionaryMessage : Dictionary<string, object>, IStorageQueueCommand
{

}
