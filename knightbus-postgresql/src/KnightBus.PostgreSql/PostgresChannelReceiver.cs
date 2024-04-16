using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace KnightBus.PostgreSql;

public class PostgresChannelReceiver<T> : IChannelReceiver
    where T : class, IPostgresCommand
{
    private PostgresQueueClient<T> _queueClient;
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageProcessor _processor;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageSerializer _serializer;
    private readonly SemaphoreSlim _maxConcurrent;
    private CancellationTokenSource _pumpDelayCancellationTokenSource = new();
    private readonly TimeSpan _pollingSleepInterval;
    private Task _messagePumpTask;

    public PostgresChannelReceiver(
        NpgsqlDataSource npgsqlDataSource,
        IMessageProcessor processor,
        IProcessingSettings settings,
        IHostConfiguration hostConfiguration,
        IMessageSerializer serializer,
        IPostgresConfiguration postgresConfiguration)
    {
        _npgsqlDataSource = npgsqlDataSource;
        _processor = processor;
        Settings = settings;
        _hostConfiguration = hostConfiguration;
        _serializer = serializer;
        _maxConcurrent = new SemaphoreSlim(settings.MaxConcurrentCalls);
        _pollingSleepInterval = postgresConfiguration.PollingSleepInterval;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _queueClient = new PostgresQueueClient<T>(_npgsqlDataSource, _serializer);

        // TODO: Use NOTIFY + LISTEN to cancel delay token?
        _messagePumpTask = Task.Factory.StartNew(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await PumpAsync(cancellationToken).ConfigureAwait(false))
                    await Delay(_pumpDelayCancellationTokenSource.Token).ConfigureAwait(false);
            }

            _hostConfiguration.Log.LogInformation("Postgres receiver cancellation requested. Disposing npgsql data source");
            await _npgsqlDataSource.DisposeAsync();
        });

        return Task.CompletedTask;
    }

    private async Task<bool> PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            var prefetchCount = Settings.PrefetchCount > 0 ? Settings.PrefetchCount : 1;
            var messages = await _queueClient
                .GetMessagesAsync(prefetchCount, (int)Settings.MessageLockTimeout.TotalSeconds, cancellationToken)
                .ConfigureAwait(false);

            if (messages.Count == 0) return false;

            foreach (var postgresMessage in messages)
            {
                await _maxConcurrent.WaitAsync(cancellationToken).ConfigureAwait(false);
                var timeoutToken = new CancellationTokenSource(Settings.MessageLockTimeout);
                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken.Token);
#pragma warning disable 4014
                Task.Run(
                        async () => await ProcessMessageAsync(postgresMessage, linkedToken.Token).ConfigureAwait(false),
                        timeoutToken.Token)
                    .ContinueWith(task2 =>
                    {
                        _maxConcurrent.Release();
                        timeoutToken.Dispose();
                        linkedToken.Dispose();
                    }).ConfigureAwait(false);
#pragma warning restore 4014
            }

            return true;
        }
        catch (PostgresException e) when (e.SqlState == "42P01")
        {
            await QueueInitializer.InitQueue(
                PostgresQueueName.Create(AutoMessageMapper.GetQueueName<T>()), _npgsqlDataSource);
            return false;
        }
        catch (Exception e)
        {
            _hostConfiguration.Log.LogError(e, "Postgres message pump error");
            return false;
        }
    }

    private async Task Delay(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_pollingSleepInterval, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            _pumpDelayCancellationTokenSource = new CancellationTokenSource();
        }
    }

    private async Task ProcessMessageAsync(PostgresMessage<T> postgresMessage, CancellationToken cancellationToken)
    {
        var stateHandler = new PostgresMessageStateHandler<T>(
            _npgsqlDataSource,
            postgresMessage,
            Settings.DeadLetterDeliveryLimit,
            _serializer,
            _hostConfiguration.DependencyInjection);

        await _processor.ProcessAsync(stateHandler, cancellationToken).ConfigureAwait(false);
    }

    public IProcessingSettings Settings { get; set; }
}
