using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    /// <summary>
    /// Client for sending messages over the <see cref="RedisTransport"/>
    /// </summary>
    public interface IRedisBus
    {
        Task SendAsync<T>(T message) where T : IRedisCommand;
        Task SendAsync<T>(IEnumerable<T> messages) where T : IRedisCommand;
        Task PublishAsync<T>(T message) where T : IRedisEvent;
        Task PublishAsync<T>(IEnumerable<T> messages) where T : IRedisEvent;
    }

    public class RedisBus : IRedisBus
    {
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly RedisConfiguration _configuration;
        private IMessageAttachmentProvider _attachmentProvider;

        public RedisBus(string connectionString) : this(new RedisConfiguration(connectionString))
        { }
        public RedisBus(RedisConfiguration configuration)
        {
            _multiplexer = ConnectionMultiplexer.Connect(configuration.ConnectionString);
            _configuration = configuration;
        }

        public void EnableAttachments(IMessageAttachmentProvider attachmentProvider)
        {
            _attachmentProvider = attachmentProvider;
        }

        public Task SendAsync<T>(T message) where T : IRedisCommand
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            return SendAsync(message, queueName);
        }
        public Task SendAsync<T>(IEnumerable<T> messages) where T : IRedisCommand
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            return SendAsync<T>(messages.ToList(), queueName);
        }

        private Task SendAsync<T>(IList<T> messages, string queueName) where T : IRedisMessage
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var serialized = messages.Select(m => (RedisValue)_configuration.MessageSerializer.Serialize(m)).ToArray();
            return Task.WhenAll(
                UploadAttachments(messages, queueName, db),
                db.ListRightPushAsync(queueName, serialized),
                db.PublishAsync(queueName, 0, CommandFlags.FireAndForget)
            );
        }

        private Task SendAsync<T>(T message, string queueName) where T : IRedisMessage
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            return Task.WhenAll(
                UploadAttachment(message, queueName, db),
                db.ListRightPushAsync(queueName, _configuration.MessageSerializer.Serialize(message)),
                db.PublishAsync(queueName, 0, CommandFlags.FireAndForget)
            );
        }

        public async Task PublishAsync<T>(T message) where T : IRedisEvent
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var subscriptions = await db.SetMembersAsync(RedisQueueConventions.GetSubscriptionKey(queueName)).ConfigureAwait(false);
            if(subscriptions == null) return;
            await Task.WhenAll(subscriptions.Select(sub => SendAsync(message, RedisQueueConventions.GetSubscriptionQueueName(queueName, sub)))).ConfigureAwait(false);
        }

        public async Task PublishAsync<T>(IEnumerable<T> messages) where T : IRedisEvent
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var subscriptions = await db.SetMembersAsync(RedisQueueConventions.GetSubscriptionKey(queueName)).ConfigureAwait(false);
            if (subscriptions == null) return;
            var messageList = messages.ToList();
            await Task.WhenAll(subscriptions.Select(sub => SendAsync<T>(messageList, RedisQueueConventions.GetSubscriptionQueueName(queueName, sub)))).ConfigureAwait(false);
        }
        

        private async Task UploadAttachment<T>(T message, string queueName, IDatabase db) where T : IRedisMessage
        {
            if (typeof(ICommandWithAttachment).IsAssignableFrom(typeof(T)))
            {
                if (_attachmentProvider == null) throw new AttachmentProviderMissingException();
                var attachmentMessage = (ICommandWithAttachment)message;
                if (attachmentMessage.Attachment != null)
                {
                    var attachmentId = Guid.NewGuid().ToString("N");
                    await db.HashSetAsync(RedisQueueConventions.GetHashKey(queueName, message.Id), AttachmentUtility.AttachmentKey, attachmentId).ConfigureAwait(false);
                    await _attachmentProvider.UploadAttachmentAsync(queueName, attachmentId, attachmentMessage.Attachment).ConfigureAwait(false);
                }
            }
        }

        private Task UploadAttachments<T>(IEnumerable<T> messages, string queueName, IDatabase db) where T : IRedisMessage
        {
            if (typeof(ICommandWithAttachment).IsAssignableFrom(typeof(T)))
            {
                return Task.WhenAll(messages.Select(m => UploadAttachment(m, queueName, db)));
            }

            return Task.CompletedTask;
        }
    }
}