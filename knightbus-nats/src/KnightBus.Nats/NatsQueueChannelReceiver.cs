using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;
using NATS.Client;

namespace KnightBus.Nats;

public class NatsQueueChannelReceiver<T> : IChannelReceiver
    where T : class, IMessage
{
    private readonly IMessageSerializer _serializer;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageProcessor _processor;
    private readonly INatsConfiguration _configuration;
    private readonly IEventSubscription _subscription;
    private const string CommandQueueGroup = "qg";
    public IProcessingSettings Settings { get; set; }
    private readonly ConnectionFactory _factory;
    private readonly ILogger _log;
    private CancellationToken _cancellationToken;
    private IConnection _connection;
    private readonly SemaphoreSlim _maxConcurrent;

    public NatsQueueChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IHostConfiguration hostConfiguration, IMessageProcessor processor, INatsConfiguration configuration, IEventSubscription subscription)
    {
        Settings = settings;
        _serializer = serializer;
        _hostConfiguration = hostConfiguration;
        _processor = processor;
        _configuration = configuration;
        _subscription = subscription;
        _log = hostConfiguration.Log;
        _factory = new ConnectionFactory();
        _maxConcurrent = new SemaphoreSlim(Settings.MaxConcurrentCalls, Settings.MaxConcurrentCalls);
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _connection = _factory.CreateConnection(_configuration.Options);

        var queueName = AutoMessageMapper.GetQueueName<T>();
        ISyncSubscription subscription;
        if (_subscription is null)
            subscription = _connection.SubscribeSync(queueName, CommandQueueGroup);
        else
            subscription = _connection.SubscribeSync(queueName, _subscription.Name);

#pragma warning disable CS4014
        Task.Run(() => ListenForMessages(subscription), cancellationToken);
#pragma warning restore CS4014

#pragma warning disable 4014
        // ReSharper disable once MethodSupportsCancellation
        Task.Run(() =>
        {
            _cancellationToken.WaitHandle.WaitOne();
            //Cancellation requested
            try
            {
                _log.LogInformation($"Closing Nats queue consumer for {typeof(T).Name}");
                subscription.Drain();
                _connection.Close();
            }
            catch (Exception)
            {
                //Swallow
            }
        });
        return Task.CompletedTask;
#pragma warning restore 4014
    }

    private async Task ListenForMessages(ISyncSubscription subscription)
    {
        while (!_cancellationToken.IsCancellationRequested)
        {

            await _maxConcurrent.WaitAsync(_cancellationToken);
            try
            {
                var msg = subscription.NextMessage();
                var messageExpiration = new CancellationTokenSource(Settings.MessageLockTimeout);
                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(messageExpiration.Token, _cancellationToken);

#pragma warning disable CS4014
                Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessMessage(msg, linkedToken.Token).ConfigureAwait(false);
                        }
                        finally
                        {
                            _maxConcurrent.Release();
                            messageExpiration.Dispose();
                            linkedToken.Dispose();
                        }

                    }, messageExpiration.Token);
#pragma warning restore CS4014
            }
            catch (Exception e)
            {
                _log.LogError(e, "Error to read message from Nats");
            }
        }
    }

    private async Task ProcessMessage(Msg msg, CancellationToken token)
    {
        try
        {

            var stateHandler = new NatsBusMessageStateHandler<T>(msg, _serializer, Settings.DeadLetterDeliveryLimit, _hostConfiguration.DependencyInjection);
            await _processor.ProcessAsync(stateHandler, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _log.LogError(e, "Nats OnMessageHandler Error");
        }
    }
}
