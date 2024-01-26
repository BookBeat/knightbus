using KnightBus.Azure.Storage.Messages;

namespace KnightBus.UI.Console.Providers.StorageBus;

public class FakeMessage : Dictionary<string, object>, IStorageQueueCommand
{
    
}
