using System;
using KnightBus.Messages;

namespace KnightBus.Core;

/// <summary>
/// Responsible for creating the transport channel implementation
/// </summary>
public interface ITransportChannelFactory
{
    ITransportConfiguration Configuration { get; set; }
    IChannelReceiver Create(Type messageType, IEventSubscription subscription, IProcessingSettings processingSettings, IMessageSerializer serializer, IHostConfiguration configuration, IMessageProcessor processor);
    bool CanCreate(Type messageType);
}
