using KnightBus.Azure.Storage.Messages;

namespace KnightBus.UI.Blazor.Providers.StorageBus;

public class FakeMessage : Dictionary<string, object>, IStorageQueueCommand
{

}
