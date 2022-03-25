using System;

namespace KnightBus.Azure.ServiceBus
{
    /// <summary>
    /// Will restart the receiver of the transport if idle (i.e. no messages processed) for TimeSpan
    /// </summary>
    public interface IRestartTransportOnIdle
    {
        public TimeSpan IdleTimeout { get; }
    }
}