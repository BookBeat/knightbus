using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using NATS.Client;

namespace KnightBus.Nats;

public class NatsTransport : ITransport
{
    public NatsTransport(string connectionString) : this(new NatsConfiguration { ConnectionString = connectionString })
    {

    }
    public NatsTransport(INatsConfiguration configuration)
    {
        TransportChannelFactories = new ITransportChannelFactory[] { new NatsChannelFactory(configuration), };
    }
    public ITransportChannelFactory[] TransportChannelFactories { get; }
    public ITransport ConfigureChannels(ITransportConfiguration configuration)
    {
        foreach (var channelFactory in TransportChannelFactories)
        {
            channelFactory.Configuration = configuration;
        }

        return this;
    }
}

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
