using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core;

/// <summary>
/// Marks a class as a listener of events published on a transport
/// </summary>
public interface IProcessEvent<TTopic, TTopicSubscription, TSettings> : IProcessMessage<TTopic, Task>
    where TTopic : IEvent
    where TTopicSubscription : class, IEventSubscription<TTopic>, new()
    where TSettings : class, IProcessingSettings, new()
{
}
