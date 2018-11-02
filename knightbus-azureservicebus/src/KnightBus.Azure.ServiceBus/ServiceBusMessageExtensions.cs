using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace KnightBus.Azure.ServiceBus
{
    internal static class ServiceBusMessageExtensions
    {
        public static Task DeadLetterByDeliveryLimitAsync(this Message message, IReceiverClient client, int deliveryLimit)
        {
            return client.DeadLetterAsync(message.SystemProperties.LockToken, "MaxDeliveryCountExceeded", $"DeliveryCount exceeded limit of {deliveryLimit}");
        }

        public static Task AbandonByErrorAsync(this Message message, IReceiverClient client, Exception e)
        {
            return client.AbandonAsync(message.SystemProperties.LockToken, new Dictionary<string, object>
            {
                {"ErrorMessage", e.Message},
                {"Exception", e.ToString()}
            });
        }
    }
}
