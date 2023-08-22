
using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using KnightBus.Nats.Tests.Integration.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace KnightBus.Nats.Tests.Integration
{
    [SetUpFixture]
    public class TestHostSetup
    {
        private IHost _knightBus;
        public static IServiceProvider ServiceProvider { get; private set; }

        [OneTimeSetUp]
        public async Task Setup()
        {
            var connectionString = "nats://localhost:4222";
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
                        .Replace<IExecutionCounter>()
                        .Replace<IExecutionCompletion>()
                        .UseBlobStorage(storageConnection)
                        .UseBlobStorageAttachments()
                        .UseBlobStorageSagas()
                        .UseJetStream(configuration => configuration.ConnectionString = connectionString)
                        .RegisterProcessors(typeof(CommandProcessor).Assembly)
                        //Enable the Nats Transport
                        .UseTransport<JetStreamTransport>();

                })
                .UseKnightBus()
                .Build();
            await _knightBus.StartAsync(CancellationToken.None);
            ServiceProvider = _knightBus.Services;
        }

        [OneTimeTearDown]
        public async Task TearDown()
        {
            await _knightBus.StopAsync();
        }
    }
}

