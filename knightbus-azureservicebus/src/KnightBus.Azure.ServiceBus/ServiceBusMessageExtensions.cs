using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace KnightBus.Azure.ServiceBus;

internal static class ServiceBusMessageExtensions
{
    public static Task DeadLetterByDeliveryLimitAsync(this ServiceBusReceivedMessage message, ProcessMessageEventArgs client, int deliveryLimit)
    {
        return client.DeadLetterMessageAsync(message, "MaxDeliveryCountExceeded", $"DeliveryCount exceeded limit of {deliveryLimit}");
    }

    public static Task AbandonByErrorAsync(this ServiceBusReceivedMessage message, ProcessMessageEventArgs client, Exception e)
    {
        return client.AbandonMessageAsync(message, new Dictionary<string, object>
        {
            {"ErrorMessage", e.Message},
            {"Exception", e.ToString()}
        });
    }
}
