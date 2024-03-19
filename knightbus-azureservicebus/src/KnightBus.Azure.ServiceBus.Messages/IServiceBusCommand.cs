using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus.Messages;

/// <summary>
/// Azure Service Bus transport mechanism, for commands that are processed fast for high performance.
/// </summary>
public interface IServiceBusCommand : ICommand
{

}
