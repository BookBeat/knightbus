﻿using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Npgsql;

namespace KnightBus.PostgreSql;

public class PostgresSubscriptionChannelReceiver<T> : IChannelReceiver
    where T : class, IPostgresEvent
{
    private PostgresSubscriptionClient<T> _queueClient;
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageProcessor _processor;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageSerializer _serializer;
    private readonly IPostgresConfiguration _postgresConfiguration;

    public PostgresSubscriptionChannelReceiver(
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
        _postgresConfiguration = postgresConfiguration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _queueClient = new PostgresSubscriptionClient<T>(_npgsqlDataSource, _serializer);
        var pump = new PostgresMessagePump<T>(Settings, _queueClient, _npgsqlDataSource, _postgresConfiguration, _hostConfiguration.Log);
        await pump.StartAsync<T>(ProcessMessageAsync, cancellationToken);
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
