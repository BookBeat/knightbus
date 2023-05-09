using KnightBus.Messages;

namespace KnightBus.Nats.Messages
{
    public interface INatsRequest : IRequest
    {
        /// <summary>
        /// Timeout, in milliseconds, when performing a request.
        /// Defaults to 30000.
        /// </summary>
        int Timeout => 30000;
    }
}
