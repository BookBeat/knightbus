using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Nats.Messages;
using NATS.Client;

namespace KnightBus.Nats
{
    public interface INatsBus
    {
        void Publish(INatsCommand command, CancellationToken cancellationToken = default);
        Task<TResponse> RequestAsync<T, TResponse>(INatsRequest request, CancellationToken cancellationToken = default) where T : INatsRequest;
        IEnumerable<TResponse> RequestStream<T, TResponse>(INatsRequest command, CancellationToken cancellationToken = default) where T : INatsRequest;
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

        public void Publish(INatsCommand command, CancellationToken cancellationToken = default)
        {

            var mapping = AutoMessageMapper.GetMapping(command.GetType());
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            _connection.Publish(mapping.QueueName, serializer.Serialize(command));
        }

        public async Task<TResponse> RequestAsync<T, TResponse>(INatsRequest request, CancellationToken cancellationToken = default) where T : INatsRequest
        {
            var mapping = AutoMessageMapper.GetMapping(request.GetType());
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            var reply = await _connection.RequestAsync(mapping.QueueName, serializer.Serialize(request), cancellationToken).ConfigureAwait(false);
            return serializer.Deserialize<TResponse>(reply.Data.AsSpan());
        }

        public IEnumerable<TResponse> RequestStream<T, TResponse>(INatsRequest command, CancellationToken cancellationToken = default) where T : INatsRequest
        {
            var mapping = AutoMessageMapper.GetMapping(command.GetType());
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            var inbox = Guid.NewGuid().ToString("N");
            var sub = _connection.SubscribeSync(inbox);
            _connection.Publish(mapping.QueueName, inbox, serializer.Serialize(command));

            do
            {
                var msg = sub.NextMessage();
                if (msg.Data.Length == 0)
                {
                    break;
                }

                yield return serializer.Deserialize<TResponse>(msg.Data.AsSpan());

            } while (true);
            sub.Unsubscribe();
            sub.Dispose();
        }
    }
}