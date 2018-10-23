using System;
using KnightBus.Core;

namespace KnightBus.Host.Singleton
{
    internal class SingletonProcessingSettings : IProcessingSettings
    {
        public int MaxConcurrentCalls => 1;
        public int PrefetchCount => 0;
        public TimeSpan MessageLockTimeout { get; set; }
        public int DeadLetterDeliveryLimit { get; set; }
    }
}