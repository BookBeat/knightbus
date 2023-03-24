using System;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host
{
    internal class PipelineInformation : IPipelineInformation
    {
        public PipelineInformation(Type processorInterfaceType, IEventSubscription subscription, IProcessingSettings processingSettings, IHostConfiguration hostConfiguration)
        {
            ProcessorInterfaceType = processorInterfaceType;
            Subscription = subscription;
            ProcessingSettings = processingSettings;
            HostConfiguration = hostConfiguration;
        }

        public Type ProcessorInterfaceType { get; }
        public IEventSubscription Subscription { get; }
        public IProcessingSettings ProcessingSettings { get; }
        public IHostConfiguration HostConfiguration { get; }
    }
}
