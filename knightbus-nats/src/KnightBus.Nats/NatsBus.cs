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
        private readonly IConnection _connection;
        private readonly INatsBusConfiguration _configuration;

        public NatsBus(IConnection connection, INatsBusConfiguration configuration)
        {
            _connection = connection;
            _configuration = configuration;
        }

        public Task SendAsync<T>(T command, CancellationToken cancellationToken = default) where T : class, INatsCommand
        {
            
            var mapping = AutoMessageMapper.GetMapping<T>();
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            _connection.Publish(mapping.QueueName, serializer.Serialize(command));
            return Task.CompletedTask;
        }

        public async Task<TReply> RequestAsync<T, TReply>(T command, CancellationToken cancellationToken = default) where T : class, INatsCommand
        {
            var mapping = AutoMessageMapper.GetMapping<T>();
            var serializer = _configuration.MessageSerializer;
            if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

            var reply = await _connection.RequestAsync(mapping.QueueName, serializer.Serialize(command), cancellationToken).ConfigureAwait(false);
            return serializer.Deserialize<TReply>(reply.Data.AsSpan());
        }
    }
}