using NUnit.Framework;
using Testcontainers.ServiceBus;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

[SetUpFixture]
internal class ServiceBusSetup
{
    // The transport cannot create missing entities on the emulator, so every one the tests use is in Config.json
    private static readonly ServiceBusContainer Emulator = new ServiceBusBuilder(
        "mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.1"
    )
        .WithAcceptLicenseAgreement(true)
        .WithConfig(Path.Combine(AppContext.BaseDirectory, "Config.json"))
        .Build();
    public static string ConnectionString = null!;

    // The emulator serves the administration API on its own port, which ConnectionString does not reach
    public static string AdministrationConnectionString = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await Emulator.StartAsync();
        ConnectionString = Emulator.GetConnectionString();
        AdministrationConnectionString = Emulator.GetHttpConnectionString();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await Emulator.DisposeAsync();
}
