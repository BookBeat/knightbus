using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.Sagas;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.PostgreSql;
using KnightBus.PostgreSql.Extensions.Azure;
using KnightBus.PostgreSql.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Examples.PostgreSql;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting PostgreSQL example");

        const string connectionString =
            "Server=localhost;Port=5432;Username=postgres;Password=password;";

        var knightBusHost = Microsoft
            .Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices(services =>
            {
                services
                    // .UsePostgres(configuration =>
                    // {
                    //     configuration.ConnectionString = connectionString;
                    //     configuration.PollingDelay = TimeSpan.FromMilliseconds(250);
                    // })
                    .UsePostgresWithAzureManagedIdentity(configuration =>
                    {
                        configuration.TokenCredential = new DefaultAzureCredential();
                        configuration.ConnectionString = connectionString;
                    })
                    // .UsePostgres(configuration => configuration.ConnectionString = "")
                    .UsePostgresSagaStore()
                    .RegisterProcessors(typeof(SamplePostgresMessage).Assembly)
                    //Enable the postgres Transport
                    .UseTransport<PostgresTransport>();
            })
            .UseKnightBus(c => c.ShutdownGracePeriod = TimeSpan.FromSeconds(2))
            .Build();

        //Start the KnightBus Host, it will now connect to the postgresql and listen
        await knightBusHost.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(5));

        var client = (PostgresBus)
            knightBusHost.Services.CreateScope().ServiceProvider.GetRequiredService<IPostgresBus>();

        // Start the saga
        await client.SendAsync(new SamplePostgresSagaStarterCommand(), CancellationToken.None);

        await client.PublishAsync(
            new SamplePostgresEvent { MessageBody = "Yo" },
            CancellationToken.None
        );
        var messages = new List<SamplePostgresMessage>();
        for (int i = 0; i < 10000; i++)
        {
            messages.Add(new SamplePostgresMessage { MessageBody = i.ToString() });
        }
        foreach (var chunk in messages.Chunk(1000))
        {
            await client.SendAsync(chunk, default);
        }

        Console.ReadKey();

        await client.SendAsync(
            new SamplePoisonPostgresMessage { MessageBody = $"error_{Guid.NewGuid()}" },
            default
        );

        Console.ReadKey();
    }
}

class SamplePostgresEvent : IPostgresEvent
{
    public required string MessageBody { get; set; }
}

class SamplePostgresMessage : IPostgresCommand
{
    public required string MessageBody { get; set; }
}

class SamplePoisonPostgresMessage : IPostgresCommand
{
    public required string MessageBody { get; set; }
}

class SamplePostgresSagaStarterCommand : IPostgresCommand
{
    public string SagaId => "abe7d7a5b99a475291aa7c7b25589308";
}

class SamplePostgresSagaCommand : IPostgresCommand
{
    public string SagaId => "abe7d7a5b99a475291aa7c7b25589308";
}

class SamplePostgresEventMapping : IMessageMapping<SamplePostgresEvent>
{
    public string QueueName => "sample_topic";
}

class SampleSubscription : IEventSubscription<SamplePostgresEvent>
{
    public string Name => "sample_subscription";
}

class SampleSubscription2 : IEventSubscription<SamplePostgresEvent>
{
    public string Name => "sample_subscription_2";
}

class SamplePostgresMessageMapping : IMessageMapping<SamplePostgresMessage>
{
    public string QueueName => "postgres_sample_message";
}

class SamplePoisonPostgresMessageMapping : IMessageMapping<SamplePoisonPostgresMessage>
{
    public string QueueName => "poisoned_postgres_sample_message";
}

class SamplePostgresSagaStarterCommandMapping : IMessageMapping<SamplePostgresSagaStarterCommand>
{
    public string QueueName => "sample_postgres_saga_start_command";
}

class SamplePostgresSagaCommandMapping : IMessageMapping<SamplePostgresSagaCommand>
{
    public string QueueName => "sample_postgres_saga_command";
}

class PostgresCommandProcessor
    : IProcessCommand<SamplePostgresMessage, PostgresProcessingSetting>,
        IProcessCommand<SamplePoisonPostgresMessage, PostgresProcessingSetting>,
        IProcessEvent<SamplePostgresEvent, SampleSubscription, PostgresProcessingSetting>,
        IProcessEvent<SamplePostgresEvent, SampleSubscription2, PostgresProcessingSetting>
{
    public Task ProcessAsync(SamplePostgresMessage message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handler 1: '{message.MessageBody}'");
        return Task.CompletedTask;
    }

    public Task ProcessAsync(
        SamplePoisonPostgresMessage message,
        CancellationToken cancellationToken
    )
    {
        Console.WriteLine($"Handler 2: '{message.MessageBody}'");
        throw new InvalidOperationException();
    }

    public Task ProcessAsync(SamplePostgresEvent message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Event 1: '{message.MessageBody}'");
        return Task.CompletedTask;
    }
}

class PostgresProcessingSetting : IProcessingSettings
{
    public int MaxConcurrentCalls => 10;
    public int PrefetchCount => 50;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
    public int DeadLetterDeliveryLimit => 2;
}

class PostgresSagaData
{
    public int Counter { get; set; }
}

class PostgresSagaProcessor
    : Saga<PostgresSagaData>,
        IProcessCommand<SamplePostgresSagaStarterCommand, PostgresProcessingSetting>,
        IProcessCommand<SamplePostgresSagaCommand, PostgresProcessingSetting>
{
    public override string PartitionKey => "postgres-saga-processor";
    public override TimeSpan TimeToLive => TimeSpan.FromHours(1);

    private readonly IPostgresBus _bus;

    public PostgresSagaProcessor(IPostgresBus bus)
    {
        _bus = bus;

        MessageMapper.MapStartMessage<SamplePostgresSagaStarterCommand>(m => m.SagaId);
        MessageMapper.MapMessage<SamplePostgresSagaCommand>(m => m.SagaId);
    }

    public async Task ProcessAsync(
        SamplePostgresSagaStarterCommand message,
        CancellationToken cancellationToken
    )
    {
        await _bus.SendAsync(new SamplePostgresSagaCommand(), cancellationToken);
    }

    public async Task ProcessAsync(
        SamplePostgresSagaCommand message,
        CancellationToken cancellationToken
    )
    {
        Data.Counter++;
        await UpdateAsync(CancellationToken.None);
        Console.WriteLine($"Saga value was {Data.Counter}");
        if (Data.Counter < 10)
        {
            await _bus.SendAsync(new SamplePostgresSagaCommand(), CancellationToken.None);
        }
        else
        {
            await CompleteAsync(CancellationToken.None);
            Console.WriteLine("Saga completed");
        }
    }
}
