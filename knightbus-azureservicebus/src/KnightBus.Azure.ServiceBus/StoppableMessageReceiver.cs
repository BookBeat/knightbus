using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace KnightBus.Azure.ServiceBus
{
    internal class StoppableMessageReceiver : MessageReceiver
    {
        public StoppableMessageReceiver(ServiceBusConnection serviceBusConnection, string entityPath, ReceiveMode receiveMode = ReceiveMode.PeekLock, RetryPolicy retryPolicy = null, int prefetchCount = 0) : base(serviceBusConnection, entityPath, receiveMode, retryPolicy, prefetchCount)
        {
        }

        public Task StopPumpAsync()
        {
            return base.OnClosingAsync();
        }
    }
}