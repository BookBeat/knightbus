using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using NATS.Client;

namespace KnightBus.Nats
{
    public interface INatsConfiguration : ITransportConfiguration
    {
        public Options Options { get; }
    }
    public class NatsConfiguration : INatsConfiguration
    {
        public string ConnectionString
        {
            get => Options?.Url;
            set => Options.Url = value;
        }

        public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
        public Options Options { get; } = ConnectionFactory.GetDefaultOptions();
    }
}
