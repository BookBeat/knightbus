using System;
using KnightBus.Messages;

namespace KnightBus.Core
{
    public interface IPipelineInformation
    {
        Type ProcessorInterfaceType { get; }
        IEventSubscription Subscription { get; }
        IProcessingSettings ProcessingSettings { get; }
        IHostConfiguration HostConfiguration { get; }
    }
}