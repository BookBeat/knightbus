using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KnightBus.LavinMQ;

/// <summary>
/// Consumes messages for a single command type or event subscription. Uses a dedicated channel with
/// an <see cref="AsyncEventingBasicConsumer"/> (push based). Concurrency is bounded by
/// <see cref="IProcessingSettings.MaxConcurrentCalls"/> via the channel's consumer dispatch concurrency
/// and a guarding semaphore; the broker prefetch is capped to the same value.
/// </summary>
public class LavinMQChannelReceiver<T> : IChannelReceiver
    where T : class, IMessage
{
    private readonly IMessageSerializer _serializer;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageProcessor _processor;
    private readonly IConnection _connection;
    private readonly IEventSubscription _subscription;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _maxConcurrent;
    private IChannel _channel = null!;
    private CancellationToken _cancellationToken;

    public IProcessingSettings Settings { get; set; }

    public LavinMQChannelReceiver(
        IProcessingSettings settings,
        IMessageSerializer serializer,
        IHostConfiguration hostConfiguration,
        IMessageProcessor processor,
        ILavinMQConfiguration configuration,
        IConnection connection,
        IEventSubscription subscription
    )
    {
        Settings = settings;
        _serializer = serializer;
        _hostConfiguration = hostConfiguration;
        _processor = processor;
        _connection = connection;
        _subscription = subscription;
        _log = hostConfiguration.Log;
        _maxConcurrent = new SemaphoreSlim(
            settings.MaxConcurrentCalls,
            settings.MaxConcurrentCalls
        );
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;

        var dispatchConcurrency = (ushort)Math.Max(1, Settings.MaxConcurrentCalls);
        _channel = await _connection
            .CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: dispatchConcurrency
                ),
                cancellationToken
            )
            .ConfigureAwait(false);

        string consumeQueue;
        if (_subscription is null)
        {
            consumeQueue = AutoMessageMapper.GetQueueName<T>();
            await LavinMQTopology
                .DeclareCommandQueueAsync(
                    _channel,
                    consumeQueue,
                    Settings.DeadLetterDeliveryLimit,
                    cancellationToken
                )
                .ConfigureAwait(false);
            await LavinMQTopology
                .BindDelayedExchangeAsync(_channel, consumeQueue, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var topic = AutoMessageMapper.GetQueueName<T>();
            consumeQueue = await LavinMQTopology
                .DeclareSubscriptionAsync(
                    _channel,
                    topic,
                    _subscription.Name,
                    Settings.DeadLetterDeliveryLimit,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        var prefetch = (ushort)
            Math.Max(1, Math.Max(Settings.MaxConcurrentCalls, Settings.PrefetchCount));
        await _channel
            .BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: prefetch,
                global: false,
                cancellationToken
            )
            .ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;
        await _channel
            .BasicConsumeAsync(consumeQueue, autoAck: false, consumer, cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.Register(() => _ = CloseAsync());
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        await _maxConcurrent.WaitAsync(_cancellationToken).ConfigureAwait(false);
        try
        {
            var stateHandler = new LavinMQMessageStateHandler<T>(
                _channel,
                eventArgs.DeliveryTag,
                eventArgs.Redelivered,
                eventArgs.Body,
                eventArgs.BasicProperties,
                _serializer,
                Settings.DeadLetterDeliveryLimit,
                _hostConfiguration.DependencyInjection
            );
            await _processor.ProcessAsync(stateHandler, _cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _log.LogError(e, "LavinMQ message processing failed for {MessageType}", typeof(T).Name);
        }
        finally
        {
            _maxConcurrent.Release();
        }
    }

    private async Task CloseAsync()
    {
        try
        {
            _log.LogInformation("Closing LavinMQ consumer for {MessageType}", typeof(T).Name);
            await _channel.CloseAsync().ConfigureAwait(false);
            await _channel.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Swallow - shutting down
        }
    }
}
