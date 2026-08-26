using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using KnightBus.Messages;
using KnightBus.PostgreSql.Management;
using KnightBus.PostgreSql.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

[TestFixture]
public class PostgresLockExtensionTests
{
    private static readonly PostgresQueueName QueueName = PostgresQueueName.Create(
        AutoMessageMapper.GetQueueName<LongRunningCommand>()
    );
    private PostgresManagementClient _managementClient = null!;
    private PostgresQueueClient<LongRunningCommand> _queueClient = null!;

    [SetUp]
    public async Task SetUp()
    {
        LongRunningProcessor.Reset();
        _managementClient = new PostgresManagementClient(
            PostgresSetup.DataSource,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() }
        );
        _queueClient = new PostgresQueueClient<LongRunningCommand>(
            PostgresSetup.DataSource,
            new MicrosoftJsonSerializer()
        );
        await QueueInitializer.InitQueue(QueueName, PostgresSetup.DataSource);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _managementClient.DeleteQueue(QueueName, default);
    }

    [Test]
    public async Task Should_process_a_long_running_message_once_when_the_lock_is_extended()
    {
        //arrange: the handler outlives three fetch locks, so only renewal keeps the row hidden
        using var host = new HostBuilder()
            .ConfigureServices(services =>
                services
                    .UsePostgres(configuration =>
                    {
                        configuration.ConnectionString = PostgresSetup.ConnectionString;
                        configuration.PollingDelay = TimeSpan.FromMilliseconds(100);
                    })
                    .AddMiddleware<ExtendMessageLockDurationMiddleware>()
                    .RegisterProcessor<LongRunningProcessor>()
                    .UseTransport<PostgresTransport>()
            )
            .UseKnightBus()
            .Build();
        await host.StartAsync();

        //act
        using (var scope = host.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IPostgresBus>();
            await bus.SendAsync(new LongRunningCommand(), default);
        }
        await LongRunningProcessor.Completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        //give a lapsed lock time to be re-fetched before judging the invocation count
        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        await host.StopAsync();

        //assert
        LongRunningProcessor.Invocations.Should().Be(1);
        var remaining = await _queueClient.GetMessagesAsync(10, 0, default).ToListAsync();
        remaining.Should().BeEmpty();
        var deadLetters = await _queueClient.PeekDeadLetterMessagesAsync(10, default).ToListAsync();
        deadLetters.Should().BeEmpty();
    }
}

public class LongRunningCommand : IPostgresCommand { }

public class LongRunningCommandMapping : IMessageMapping<LongRunningCommand>
{
    public string QueueName => "lock_extension_test";
}

public class LongRunningSettings : IProcessingSettings, IExtendMessageLockTimeout
{
    public int MaxConcurrentCalls => 4;
    public int PrefetchCount => 0;
    public TimeSpan MessageLockTimeout => TimeSpan.FromSeconds(30);
    public int DeadLetterDeliveryLimit => 5;
    public TimeSpan ExtensionDuration => TimeSpan.FromSeconds(1);
    public TimeSpan ExtensionInterval => TimeSpan.FromMilliseconds(300);
}

public class LongRunningProcessor : IProcessCommand<LongRunningCommand, LongRunningSettings>
{
    private static int _invocations;

    public static int Invocations => _invocations;
    public static TaskCompletionSource Completed { get; private set; } = new();

    public static void Reset()
    {
        _invocations = 0;
        Completed = new TaskCompletionSource();
    }

    public async Task ProcessAsync(LongRunningCommand message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _invocations);
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        Completed.TrySetResult();
    }
}
