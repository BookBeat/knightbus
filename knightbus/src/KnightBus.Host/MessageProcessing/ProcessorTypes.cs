using System;

namespace KnightBus.Host.MessageProcessing
{
    internal class ProcessorTypes
    {
        public ProcessorTypes(Type messageType, Type responseType, Type subscriptionType, Type settingsType)
        {
            MessageType = messageType;
            ResponseType = responseType;
            SubscriptionType = subscriptionType;
            SettingsType = settingsType;
        }

        public Type MessageType { get; }
        public Type ResponseType { get; }
        public Type SubscriptionType { get; }
        public Type SettingsType { get; }
    }
}