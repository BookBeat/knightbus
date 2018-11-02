using System;
using System.Collections.Generic;

namespace KnightBus.Core
{
    /// <summary>
    /// Responsible for creating the transport channel implementation
    /// </summary>
    public interface ITransportChannelFactory
    {
        ITransportConfiguration Configuration { get; set; }
        IList<IMessageProcessorMiddleware> Middlewares { get; }
        IChannelReceiver Create(Type messageType, Type subscriptionType, Type settingsType, IHostConfiguration configuration, IMessageProcessor processor);
        bool CanCreate(Type messageType);
    }
}