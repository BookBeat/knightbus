using KnightBus.Core;

namespace KnightBus.Azure.ServiceBus
{
    public interface IServiceBusConfiguration : ITransportConfiguration
    {
        DefaultServiceBusCreationOptions CreationOptions { get; }
    }
}
