using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;
using Microsoft.Azure.ServiceBus;

namespace KnightBus.Azure.ServiceBus
{
    public interface IServiceBus
    {
        /// <summary>
        /// Schedules a queue message for delivery a certain time into the future
        /// </summary>
        Task ScheduleAsync<T>(T message, TimeSpan span) where T : IServiceBusCommand;

        /// <summary>
        /// Sends a queue message immediately 
        /// </summary>
        Task SendAsync<T>(T message) where T : IServiceBusCommand;

        /// <summary>
        /// Sends a batch of messages using the batch send method
        /// </summary>
        Task SendAsync<T>(IList<T> messages) where T : IServiceBusCommand;

        /// <summary>
        /// Sends a topic message immediately
        /// </summary>
        Task PublishEventAsync<T>(T message) where T : IServiceBusEvent;
    }

    public class ServiceBus : IServiceBus
    {
        private readonly IServiceBusConfiguration _configuration;
        private readonly IClientFactory _clientFactory;
        private IMessageAttachmentProvider _attachmentProvider;

        public void EnableAttachments(IMessageAttachmentProvider attachmentProvider)
        {
            _attachmentProvider = attachmentProvider;
        }

        public ServiceBus(IServiceBusConfiguration configuration)
        {
            _configuration = configuration;
            _clientFactory = new ClientFactory(configuration.ConnectionString);
        }

        public async Task SendAsync<T>(T message) where T : IServiceBusCommand
        {
            var client = await _clientFactory.GetQueueClient<T>().ConfigureAwait(false);
            var sbMessage = await CreateMessageAsync(message).ConfigureAwait(false);

            await client.SendAsync(sbMessage).ConfigureAwait(false);
        }

        public async Task SendAsync<T>(IList<T> messages) where T : IServiceBusCommand
        {
            var client = await _clientFactory.GetQueueClient<T>().ConfigureAwait(false);
            if(!messages.Any()) return;
            var sbMessages = new List<Message>();
            foreach (var message in messages)
            {
                sbMessages.Add(await CreateMessageAsync(message).ConfigureAwait(false));
            }

            await client.SendAsync(sbMessages).ConfigureAwait(false);
        }

        public async Task ScheduleAsync<T>(T message, TimeSpan span) where T : IServiceBusCommand
        {
            var client = await _clientFactory.GetQueueClient<T>().ConfigureAwait(false);
            var sbMessage = await CreateMessageAsync(message).ConfigureAwait(false);
            sbMessage.ScheduledEnqueueTimeUtc = DateTime.UtcNow.Add(span);

            await client.SendAsync(sbMessage).ConfigureAwait(false);
        }

        public async Task PublishEventAsync<T>(T message) where T : IServiceBusEvent
        {
            var client = await _clientFactory.GetTopicClient<T>().ConfigureAwait(false);
            var brokeredMessage = await CreateMessageAsync(message).ConfigureAwait(false);
            await client.SendAsync(brokeredMessage).ConfigureAwait(false);
        }

        private async Task<Message> CreateMessageAsync<T>(T body) where T : IMessage
        {
            var serialized = _configuration.MessageSerializer.Serialize(body);
            var message = new Message(Encoding.UTF8.GetBytes(serialized))
            {
                ContentType = _configuration.MessageSerializer.ContentType
            };

            if (typeof(ICommandWithAttachment).IsAssignableFrom(typeof(T)))
            {
                if(_attachmentProvider == null) throw new AttachmentProviderMissingException();

                var attachmentMessage = (ICommandWithAttachment)body;
                if (attachmentMessage.Attachment != null)
                {
                    var attachmentIds = new List<string>();
                    var id = Guid.NewGuid().ToString("N");
                    var queueName = AutoMessageMapper.GetQueueName<T>();
                    await _attachmentProvider.UploadAttachmentAsync(queueName, id, attachmentMessage.Attachment).ConfigureAwait(false);
                    attachmentIds.Add(id);
                    message.UserProperties[AttachmentUtility.AttachmentKey] = string.Join(",", attachmentIds);
                }
            }

            return message;
        }
    }
}