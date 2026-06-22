using System;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ;

public interface ILavinMQConfiguration : ITransportConfiguration
{
    /// <summary>
    /// Base URL of the LavinMQ HTTP management API used by the management client, e.g.
    /// <c>http://localhost:15672</c>. When null it is derived from <see cref="ITransportConfiguration.ConnectionString"/>
    /// (same host, port 15672).
    /// </summary>
    string? ManagementApiUrl { get; set; }

    /// <summary>
    /// Optional hook to customize the <see cref="ConnectionFactory"/> before the shared connection is
    /// opened, applied after the transport's defaults (automatic connection and topology recovery are
    /// enabled). Use it to tune the recovery interval, heartbeat, TLS, etc.
    /// </summary>
    Action<ConnectionFactory>? ConfigureConnectionFactory { get; set; }
}

public class LavinMQConfiguration : ILavinMQConfiguration
{
    public LavinMQConfiguration() { }

    public LavinMQConfiguration(string connectionString)
    {
        ConnectionString = connectionString;
    }

    /// <summary>AMQP connection string, e.g. <c>amqp://guest:guest@localhost:5672</c>.</summary>
    public string ConnectionString { get; set; } = null!;

    public string? ManagementApiUrl { get; set; }

    public Action<ConnectionFactory>? ConfigureConnectionFactory { get; set; }

    public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
}
