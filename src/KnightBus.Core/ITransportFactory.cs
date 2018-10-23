using System;

namespace KnightBus.Core
{
    /// <summary>
    /// Responsible for creating the transport implementation
    /// </summary>
    public interface ITransportFactory
    {
        ITransportConfiguration Configuration { get; }
        IStartTransport Create(Type messageType, Type subscriptionType, Type settingsType, IHostConfiguration configuration, IMessageProcessor processor);
        bool CanCreate(Type messageType);
    }
}