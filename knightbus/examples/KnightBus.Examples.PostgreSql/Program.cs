using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.PostgreSql;
using KnightBus.PostgreSql.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Examples.PostgreSql;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting PostgreSQL example");

        const string connectionString = "";

        var knightBusHost = global::Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices(services =>
            {
                services
                    .UsePostgres(configuration =>
                    {
                        configuration.ConnectionString = connectionString;
                        configuration.PollingSleepInterval = TimeSpan.FromMilliseconds(250);
                    })
                    .RegisterProcessors(typeof(SamplePostgresMessage).Assembly)
                    //Enable the postgres Transport
                    .UseTransport<PostgresTransport>();
            })
            .UseKnightBus(c => c.ShutdownGracePeriod = TimeSpan.FromSeconds(2))
            .Build();

        //Start the KnightBus Host, it will now connect to the postgresql and listen
        await knightBusHost.StartAsync();

        var client =
            (PostgresBus)knightBusHost.Services.CreateScope().ServiceProvider.GetRequiredService<IPostgresBus>();

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

        await client.SendAsync(new SamplePoisonPostgresMessage { MessageBody = $"error_{Guid.NewGuid()}" }, default );

        Console.ReadKey();

        await knightBusHost.StopAsync();
    }
}

class SamplePostgresMessage : IPostgresCommand
{
    public required string MessageBody { get; set; }
}

class SamplePoisonPostgresMessage : IPostgresCommand
{
    public required string MessageBody { get; set; }
}

class SamplePostgresMessageMapping : IMessageMapping<SamplePostgresMessage>
{
    public string QueueName => "postgres_sample_message";
}

class SamplePoisonPostgresMessageMapping : IMessageMapping<SamplePoisonPostgresMessage>
{
    public string QueueName => "poisoned_postgres_sample_message";
}

class PostgresCommandProcessor :
    IProcessCommand<SamplePostgresMessage, PostgresProcessingSetting>,
    IProcessCommand<SamplePoisonPostgresMessage, PostgresProcessingSetting>
{
    public Task ProcessAsync(SamplePostgresMessage message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handler 1: '{message.MessageBody}'");
        return Task.CompletedTask;
    }

    public Task ProcessAsync(SamplePoisonPostgresMessage message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handler 2: '{message.MessageBody}'");
        throw new InvalidOperationException();
    }
}

class PostgresProcessingSetting : IProcessingSettings
{
    public int MaxConcurrentCalls => 20;
    public int PrefetchCount => 50;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
    public int DeadLetterDeliveryLimit => 2;
}
