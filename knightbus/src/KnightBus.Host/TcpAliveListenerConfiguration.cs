namespace KnightBus.Host
{
    public interface ITcpAliveListenerConfiguration
    {
        int Port { get; }
    }

    internal class TcpAliveListenerConfiguration : ITcpAliveListenerConfiguration
    {
        public TcpAliveListenerConfiguration(int port)
        {
            Port = port;
        }

        public int Port { get; }
    }
}