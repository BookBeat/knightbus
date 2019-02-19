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
using Microsoft.Azure.ServiceBus.Core;

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
        private IMessageAttachmentProvider _attachmentProvider;
        private const int DefaultRetryCount = 3;

        internal IClientFactory ClientFactory { get; set; }

        public void EnableAttachments(IMessageAttachmentProvider attachmentProvider)
        {
            _attachmentProvider = attachmentProvider;
        }

        public ServiceBus(IServiceBusConfiguration config)
        {
            ClientFactory = new ClientFactory(config.ConnectionString);
            _configuration = config;
        }

        public async Task SendAsync<T>(T message) where T : IServiceBusCommand
        {
            var client = await ClientFactory.GetQueueClient<T>().ConfigureAwait(false);
            var sbMessage = await CreateMessageAsync(message).ConfigureAwait(false);

            await SendAsync(client, sbMessage, DefaultRetryCount).ConfigureAwait(false);
        }

        public async Task SendAsync<T>(IList<T> messages) where T : IServiceBusCommand
        {
            var client = await ClientFactory.GetQueueClient<T>().ConfigureAwait(false);
            if (!messages.Any()) return;
            var sbMessages = new List<Message>();
            foreach (var message in messages)
            {
                sbMessages.Add(await CreateMessageAsync(message).ConfigureAwait(false));
            }

            await SendAsync(client, sbMessages, DefaultRetryCount).ConfigureAwait(false);
        }

        public async Task ScheduleAsync<T>(T message, TimeSpan span) where T : IServiceBusCommand
        {
            var client = await ClientFactory.GetQueueClient<T>().ConfigureAwait(false);
            var sbMessage = await CreateMessageAsync(message).ConfigureAwait(false);
            sbMessage.ScheduledEnqueueTimeUtc = DateTime.UtcNow.Add(span);

            await ScheduleAsync(client, sbMessage, DefaultRetryCount).ConfigureAwait(false);
        }

        public async Task PublishEventAsync<T>(T message) where T : IServiceBusEvent
        {
            var client = await ClientFactory.GetTopicClient<T>().ConfigureAwait(false);
            var brokeredMessage = await CreateMessageAsync(message).ConfigureAwait(false);

            await PublishEventAsync(client, brokeredMessage, DefaultRetryCount).ConfigureAwait(false);
        }

        private async Task SendAsync(ISenderClient client, Message message, int retryCount)
        {
            try
            {
                await client.SendAsync(message).ConfigureAwait(false);
            }
            catch (ServiceBusCommunicationException) when (retryCount > 0)
            {
                await Task.Delay(GetRetryDelay(retryCount)).ConfigureAwait(false);
                await SendAsync(client, message, retryCount - 1).ConfigureAwait(false);
            }
        }

        private async Task SendAsync(ISenderClient client, IList<Message> messages, int retryCount)
        {
            try
            {
                await client.SendAsync(messages).ConfigureAwait(false);
            }
            catch (ServiceBusCommunicationException) when (retryCount > 0)
            {
                await Task.Delay(GetRetryDelay(retryCount)).ConfigureAwait(false);
                await SendAsync(client, messages, retryCount - 1).ConfigureAwait(false);
            }
        }

        private async Task ScheduleAsync(ISenderClient client, Message message, int retryCount)
        {
            try
            {
                await client.SendAsync(message).ConfigureAwait(false);
            }
            catch (ServiceBusCommunicationException) when (retryCount > 0)
            {
                await Task.Delay(GetRetryDelay(retryCount)).ConfigureAwait(false);
                await ScheduleAsync(client, message, retryCount - 1).ConfigureAwait(false);
            }
        }

        private async Task PublishEventAsync(ISenderClient client, Message message, int retryCount) 
        {
            try
            {
                await client.SendAsync(message).ConfigureAwait(false);
            }
            catch (ServiceBusCommunicationException) when (retryCount > 0)
            {
                await Task.Delay(GetRetryDelay(retryCount)).ConfigureAwait(false);
                await PublishEventAsync(client, message, retryCount - 1).ConfigureAwait(false);
            }
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
                if (_attachmentProvider == null) throw new AttachmentProviderMissingException();

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

        private TimeSpan GetRetryDelay(int count)
        {
            switch (count)
            {
                case 3: return TimeSpan.FromMilliseconds(300);
                case 2: return TimeSpan.FromMilliseconds(600);
                case 1: return TimeSpan.FromMilliseconds(900);
                default: return TimeSpan.FromMilliseconds(300);
            }
        }
    }
}