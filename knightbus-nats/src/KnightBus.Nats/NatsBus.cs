using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using NATS.Client;

namespace KnightBus.Nats
{
    public interface INatsBus
    {
        Task SendAsync<T>(T command, CancellationToken cancellationToken = default) where T : class, INatsCommand;
        Task<TReply> RequestAsync<T, TReply>(T command, CancellationToken cancellationToken = default) where T : class, INatsCommand;
    }
    public class NatsBus : INatsBus
    {
        private readonly INatsBusConfiguration _configuration;
        private readonly ConnectionFactory _factory;

        public NatsBus(INatsBusConfiguration configuration)
        {
            _configuration = configuration;
            _factory = new ConnectionFactory();
        }

        public Task SendAsync<T>(T command, CancellationToken cancellationToken = default) where T : class, INatsCommand
        {
            using(var connection = _factory.CreateConnection())
            {
                var queueName = AutoMessageMapper.GetQueueName<T>();
                
                var mapping = AutoMessageMapper.GetMapping<T>();
                var serializer = _configuration.MessageSerializer;
                if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

                connection.Publish(queueName, serializer.Serialize(command));
            }
            
            return Task.CompletedTask;
        }

        public async Task<TReply> RequestAsync<T, TReply>(T command, CancellationToken cancellationToken = default) where T : class, INatsCommand
        {
            var options = ConnectionFactory.GetDefaultOptions();
            options.ClosedEventHandler = (sender, args) => { };
            options.DisconnectedEventHandler = (sender, args) => { };
            using (var connection = _factory.CreateConnection(options))
            {
                var queueName = AutoMessageMapper.GetQueueName<T>();

                var mapping = AutoMessageMapper.GetMapping<T>();
                var serializer = _configuration.MessageSerializer;
                if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

               var reply = await connection.RequestAsync(queueName, serializer.Serialize(command), cancellationToken).ConfigureAwait(false);
               return serializer.Deserialize<TReply>(reply.Data.AsSpan());
            }
        }
    }
}