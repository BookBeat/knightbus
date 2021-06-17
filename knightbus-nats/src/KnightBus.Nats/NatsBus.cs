using System;
using System.Text;
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
    }
    public class NatsBus : INatsBus
    {
        private readonly INatsBusConfiguration _configuration;

        public NatsBus(INatsBusConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task SendAsync<T>(T command, CancellationToken cancellationToken = default) where T : class, INatsCommand
        {
            var factory = new ConnectionFactory();
            using(var connection = factory.CreateConnection())
            {
                var queueName = AutoMessageMapper.GetQueueName<T>();
                
                var mapping = AutoMessageMapper.GetMapping<T>();
                var serializer = _configuration.MessageSerializer;
                if (mapping is ICustomMessageSerializer customSerializer) serializer = customSerializer.MessageSerializer;

                connection.Publish(queueName, serializer.Serialize(command));
            }
            
            return Task.CompletedTask;
        }
    }
}