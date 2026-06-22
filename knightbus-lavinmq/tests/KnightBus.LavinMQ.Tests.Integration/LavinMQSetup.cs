using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using NUnit.Framework;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ.Tests.Integration;

[SetUpFixture]
public class LavinMQSetup
{
    private static readonly IContainer Lavin = new ContainerBuilder()
        .WithImage("cloudamqp/lavinmq:latest")
        .WithPortBinding(5672, true)
        .WithPortBinding(15672, true)
        .WithWaitStrategy(
            Wait.ForUnixContainer().UntilPortIsAvailable(5672).UntilPortIsAvailable(15672)
        )
        .Build();

    public static string ConnectionString { get; private set; } = null!;
    public static ILavinMQConfiguration Configuration { get; private set; } = null!;
    public static IConnection Connection { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        await Lavin.StartAsync();

        var amqpPort = Lavin.GetMappedPublicPort(5672);
        var managementPort = Lavin.GetMappedPublicPort(15672);
        ConnectionString = $"amqp://guest:guest@{Lavin.Hostname}:{amqpPort}";
        Configuration = new LavinMQConfiguration(ConnectionString)
        {
            ManagementApiUrl = $"http://{Lavin.Hostname}:{managementPort}",
        };

        var factory = new ConnectionFactory { Uri = new Uri(ConnectionString) };
        Connection = await factory.CreateConnectionAsync();
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        if (Connection is not null)
            await Connection.DisposeAsync();
        await Lavin.DisposeAsync();
    }
}
