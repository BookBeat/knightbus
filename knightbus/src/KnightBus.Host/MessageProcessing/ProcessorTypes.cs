using System;

namespace KnightBus.Host.MessageProcessing
{
    internal struct ProcessorTypes
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

        public override string ToString()
        {
            if(SubscriptionType == null && ResponseType == null)
                return $"<{MessageType.Name}, {SettingsType.Name}>";

            return $"<{MessageType.Name}, {ResponseType?.Name ?? SubscriptionType?.Name}, {SettingsType.Name}>";
        }
    }
}