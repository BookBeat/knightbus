using Azure.Messaging.ServiceBus;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using KnightBus.Shared.Tests.Integration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

[TestFixture]
public class ServiceBusChannelReceiverTests : ChannelReceiverTests
{
    private static readonly string Queue = AutoMessageMapper.GetQueueName<DrainCommand>();
    private ServiceBusClient _client = null!;
    private IHost _host = null!;

    //The host only supplies the client factory and the bus, the receiver is started by hand
    [OneTimeSetUp]
    public async Task StartHost()
    {
        _client = new ServiceBusClient(ServiceBusSetup.ConnectionString);
        _host = new HostBuilder()
            .ConfigureServices(services =>
                services
                    .UseServiceBus(new ServiceBusConfiguration(ServiceBusSetup.ConnectionString))
                    .UseTransport<ServiceBusTransport>()
            )
            .UseKnightBus()
            .Build();
        await _host.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopHost()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _client.DisposeAsync();
    }

    public override Task Setup() => EmulatorEntities.PurgeQueue(_client, Queue);

    protected override async Task SendMessage(string message)
    {
        using var scope = _host.Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<IServiceBus>()
            .SendAsync(new DrainCommand { Message = message });
    }

    protected override IChannelReceiver CreateReceiver(IMessageProcessor processor) =>
        new ServiceBusQueueChannelReceiver<DrainCommand>(
            Settings,
            new MicrosoftJsonSerializer(),
            new ServiceBusConfiguration(ServiceBusSetup.ConnectionString),
            _host.Services.GetRequiredService<IHostConfiguration>(),
            processor
        );
}
