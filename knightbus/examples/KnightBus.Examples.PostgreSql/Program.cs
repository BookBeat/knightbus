using System;
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
                    .UsePostgres(configuration => { configuration.ConnectionString = connectionString; })
                    .RegisterProcessors(typeof(SamplePostgresMessage).Assembly)
                    //Enable the postgres Transport
                    .UseTransport<PostgresTransport>();
            })
            .UseKnightBus()
            .Build();

        //Start the KnightBus Host, it will now connect to the postgresql and listen
        await knightBusHost.StartAsync();

        await Task.Delay(3000);

        var client =
            (PostgresBus)knightBusHost.Services.CreateScope().ServiceProvider.GetRequiredService<IPostgresBus>();

        await client.SendAsync(new SamplePostgresMessage { MessageBody = Guid.NewGuid().ToString() });
        await client.SendAsync(new SamplePoisonPostgresMessage { MessageBody = $"error_{Guid.NewGuid()}" } );

        Console.ReadKey();
        await knightBusHost.StopAsync();
    }
}

class SamplePostgresMessage : IPostgresCommand
{
    public string MessageBody { get; set; }
}

class SamplePoisonPostgresMessage : IPostgresCommand
{
    public string MessageBody { get; set; }
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
