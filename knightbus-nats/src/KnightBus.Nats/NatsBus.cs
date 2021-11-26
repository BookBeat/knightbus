using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using NATS.Client;

namespace KnightBus.Nats
{
    public interface INatsBus
    {
        Task PublishAsync(INatsMessage command, CancellationToken cancellationToken = default);
        Task<T> RequestAsync<T>(INatsRequest<T> command, CancellationToken cancellationToken = default) where T : INatsReponse;
    }

    public class NatsBus : INatsBus
    {
        private readonly IConnection _connection;
        private readonly INatsBusConfiguration _configuration;

        public NatsBus(IConnection connection, INatsBusConfiguration configuration)
        {
            _connection = connection;
            _configuration = configuration;
        }

        public Task PublishAsync(INatsMessage command, CancellationToken cancellationToken = default)
        {
            
            var mapping = AutoMessageMapper.GetMapping(command.GetType());
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            _connection.Publish(mapping.QueueName, serializer.Serialize(command));
            return Task.CompletedTask;
        }

        public async Task<T> RequestAsync<T>(INatsRequest<T> command, CancellationToken cancellationToken = default) where T : INatsReponse
        {
            var mapping = AutoMessageMapper.GetMapping(command.GetType());
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            var reply = await _connection.RequestAsync(mapping.QueueName, serializer.Serialize(command), cancellationToken).ConfigureAwait(false);
            return serializer.Deserialize<T>(reply.Data.AsSpan());
        }

        public IEnumerable<T> RequestStreamAsync<T>(INatsRequest<T> command, CancellationToken cancellationToken = default) where T : INatsReponse
        {
            var mapping = AutoMessageMapper.GetMapping(command.GetType());
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;
            
            var inbox = Guid.NewGuid().ToString("N");
            var sub = _connection.SubscribeSync(inbox);
            _connection.Publish(mapping.QueueName, inbox, serializer.Serialize(command));
            
            var stop = false;
            do
            {
                var msg = sub.NextMessage();
                stop = true;
                if (msg.Data.Length == 0)
                {
                    stop = true;
                }
                else
                {
                    yield return serializer.Deserialize<T>(msg.Data.AsSpan());
                }
                
            } while (!stop);
            sub.Unsubscribe();
            sub.Dispose();
        }
    }
}