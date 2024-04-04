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

        var connectionString =
            "Server=127.0.0.1;" +
            "Port=5432;" +
            "Database=knightbus;" +
            "User Id=postgres;" +
            "Password=passw;" +
            "Include Error Detail=true;" +
            "SearchPath=knightbus";

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
        await knightBusHost.StartAsync(CancellationToken.None);

        var client =
            (PostgresBus)knightBusHost.Services.CreateScope().ServiceProvider.GetRequiredService<IPostgresBus>();
        await client.SendAsync(new SamplePostgresMessage { MessageBody = Guid.NewGuid().ToString() });

        Console.ReadKey();
    }
}

class SamplePostgresMessage : IPostgresCommand
{
    public string MessageBody { get; set; }
}

class SamplePostgresMessageMapping : IMessageMapping<SamplePostgresMessage>
{
    public string QueueName => "sample_message";
}

class RedisEventProcessor : IProcessCommand<SamplePostgresMessage, PostgresProcessingSetting>
{
    public Task ProcessAsync(SamplePostgresMessage message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Handler 1: '{message.MessageBody}'");
        return Task.CompletedTask;
    }
}

class PostgresProcessingSetting : IProcessingSettings
{
    public int MaxConcurrentCalls => 1;
    public int PrefetchCount => 10;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
    public int DeadLetterDeliveryLimit => 5;
}
