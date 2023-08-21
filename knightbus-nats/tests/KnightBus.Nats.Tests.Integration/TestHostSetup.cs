
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace KnightBus.Nats.Tests.Integration
{
    [SetUpFixture]
    public class TestHostSetup
    {
        private IHost _knightBus;

        [OneTimeSetUp]
        public async Task Setup()
        {
            var connectionString = "nats://0.0.0.0:4222";
            var storageConnection = "UseDevelopmentStorage=true";
            // Start nats.io first
            // $ docker run -p 4222:4222 -ti -js nats:latest

            _knightBus = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
                })
                .ConfigureServices(services =>
                {
                    services
                        .UseBlobStorage(storageConnection)
                        .UseBlobStorageAttachments()
                        .UseBlobStorageSagas()
                        .UseJetStream(configuration => configuration.ConnectionString = connectionString)
                        .RegisterProcessors(typeof(CommandBehavior).Assembly)
                        //Enable the Nats Transport
                        .UseTransport<JetStreamTransport>();

                })
                .UseKnightBus()
                .Build();
            //Start the KnightBus Host, it will now connect to the StorageBus and listen to the SampleStorageBusMessageMapping.QueueName
            await _knightBus.StartAsync(CancellationToken.None);
        }

        [OneTimeTearDown]
        public async Task TearDown()
        {
            await _knightBus.StopAsync(TimeSpan.FromSeconds(10));
        }
    }
}

