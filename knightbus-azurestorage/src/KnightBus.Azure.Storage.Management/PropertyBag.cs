using System.Collections.Generic;
using KnightBus.Azure.Storage.Messages;

namespace KnightBus.Azure.Storage.Management;

public class PropertyBag : Dictionary<string, object>, IStorageQueueCommand
{

}
