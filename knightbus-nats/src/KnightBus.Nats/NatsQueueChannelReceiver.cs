using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using NATS.Client;

namespace KnightBus.Nats
{
    public class NatsQueueChannelReceiver<T> : IChannelReceiver
        where T : class, ICommand
    {
        private readonly IProcessingSettings _settings;
        private readonly IMessageSerializer _serializer;
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageProcessor _processor;
        private readonly INatsBusConfiguration _configuration;
        private readonly ConnectionFactory _factory;
        private readonly ILog _log;
        private CancellationToken _cancellationToken;
        private IConnection _connection;

        public NatsQueueChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IHostConfiguration hostConfiguration, IMessageProcessor processor, INatsBusConfiguration configuration)
        {
            _settings = settings;
            _serializer = serializer;
            _hostConfiguration = hostConfiguration;
            _processor = processor;
            _configuration = configuration;
            _log = hostConfiguration.Log;
            _factory = new ConnectionFactory();
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;

            _connection = _factory.CreateConnection();
            var queueName = AutoMessageMapper.GetQueueName<T>();

            var a = _connection.SubscribeAsync(queueName, queueName, async (sender, args) =>
            {
                var stateHandler = new NatsBusMessageStateHandler<T>(args, _serializer, 5, _hostConfiguration.DependencyInjection);
                await _processor.ProcessAsync(stateHandler, _cancellationToken).ConfigureAwait(false);
            });
            
#pragma warning disable 4014
            // ReSharper disable once MethodSupportsCancellation
            Task.Run(async () =>
            {
                _cancellationToken.WaitHandle.WaitOne();
                //Cancellation requested
                try
                {
                    _log.Information($"Closing Nats queue consumer for {typeof(T).Name}");
                    _connection.Close();
                }
                catch (Exception)
                {
                    //Swallow
                }
            });
#pragma warning restore 4014
        }
        public IProcessingSettings Settings { get; set; }
    }
}