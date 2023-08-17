using KnightBus.Messages;
using KnightBus.Newtonsoft;
using NATS.Client;
using NATS.Client.JetStream;

namespace KnightBus.Nats
{
    public interface IJetStreamConfiguration : INatsConfiguration
    {
        public JetStreamOptions JetStreamOptions { get; }
    }

    public class JetStreamConfiguration : IJetStreamConfiguration
    {
        public string ConnectionString
        {
            get => Options?.Url;
            set => Options.Url = value;
        }

        public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
        public Options Options { get; } = ConnectionFactory.GetDefaultOptions();
        public JetStreamOptions JetStreamOptions { get; set; }
    }
}
