using AwesomeAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.PostgreSql.Management;
using KnightBus.PostgreSql.Messages;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

[TestFixture]
public class PostgresConnectionPoolTests
{
    private static readonly PostgresQueueName QueueName = PostgresQueueName.Create(
        AutoMessageMapper.GetQueueName<PoolCommand>()
    );
    private PostgresManagementClient _managementClient = null!;
    private string _singleConnectionString = null!;

    [SetUp]
    public async Task SetUp()
    {
        PoolProcessor.Reset();
        _managementClient = new PostgresManagementClient(
            PostgresSetup.DataSource,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() }
        );
        _singleConnectionString = new NpgsqlConnectionStringBuilder(PostgresSetup.ConnectionString)
        {
            MaxPoolSize = 1,
            Timeout = 2,
        }.ConnectionString;
        await QueueInitializer.InitQueue(QueueName, PostgresSetup.DataSource);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _managementClient.DeleteQueue(QueueName, default);
    }

    [Test]
    public async Task Should_release_the_fetch_connection_before_the_messages_are_handed_out()
    {
        //arrange
        await SendMessages(2);
        await using var dataSource = NpgsqlDataSource.Create(_singleConnectionString);
        var client = new PostgresQueueClient<PoolCommand>(
            dataSource,
            new MicrosoftJsonSerializer()
        );
        await using var messages = client
            .GetMessagesAsync(2, TimeSpan.FromSeconds(30), default)
            .GetAsyncEnumerator();
        (await messages.MoveNextAsync()).Should().BeTrue();

        //act & assert: the only connection in the pool must be free while the batch is consumed
        await client.Awaiting(c => c.CompleteAsync(messages.Current)).Should().NotThrowAsync();
    }

    [Test]
    public async Task Should_handle_a_prefetched_batch_on_a_single_connection_pool()
    {
        //arrange: the prefetched messages wait for the one handler slot while it completes on the pool
        await SendMessages(PoolProcessor.Expected);
        using var host = new HostBuilder()
            .ConfigureServices(services =>
                services
                    .UsePostgres(configuration =>
                    {
                        configuration.ConnectionString = _singleConnectionString;
                        configuration.PollingDelay = TimeSpan.FromMilliseconds(100);
                    })
                    .RegisterProcessor<PoolProcessor>()
                    .UseTransport<PostgresTransport>()
            )
            .UseKnightBus()
            .Build();

        //act
        await host.StartAsync();
        await PoolProcessor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await host.StopAsync();

        //assert
        PoolProcessor.Invocations.Should().Be(PoolProcessor.Expected);
        //counts locked rows too, so a message handled but never completed is caught
        var queue = await _managementClient.GetQueue(QueueName, default);
        queue.ActiveMessagesCount.Should().Be(0);
        queue.DeadLetterMessagesCount.Should().Be(0);
    }

    private static async Task SendMessages(int count)
    {
        var bus = new PostgresBus(
            PostgresSetup.DataSource,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() },
            []
        );
        await bus.SendAsync(Enumerable.Range(0, count).Select(_ => new PoolCommand()), default);
    }
}

public class PoolCommand : IPostgresCommand { }

public class PoolCommandMapping : IMessageMapping<PoolCommand>
{
    public string QueueName => "connection_pool_test";
}

public class PoolSettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 1;
    public int PrefetchCount => 3;
    public TimeSpan MessageLockTimeout => TimeSpan.FromSeconds(30);
    public int DeadLetterDeliveryLimit => 5;
}

public class PoolProcessor : IProcessCommand<PoolCommand, PoolSettings>
{
    public const int Expected = 10;
    private static int _invocations;

    public static int Invocations => _invocations;
    public static TaskCompletionSource Completed { get; private set; } = new();

    public static void Reset()
    {
        _invocations = 0;
        Completed = new TaskCompletionSource();
    }

    public async Task ProcessAsync(PoolCommand message, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        if (Interlocked.Increment(ref _invocations) == Expected)
            Completed.TrySetResult();
    }
}
