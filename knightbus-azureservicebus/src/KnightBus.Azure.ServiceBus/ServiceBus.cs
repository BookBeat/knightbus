using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus
{
    public interface IServiceBus
    {
        /// <summary>
        /// Schedules a queue message for delivery a certain time into the future
        /// </summary>
        Task ScheduleAsync<T>(T message, TimeSpan span, CancellationToken cancellationToken = default) where T : IServiceBusCommand;

        /// <summary>
        /// Sends a queue message immediately 
        /// </summary>
        Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : IServiceBusCommand;

        /// <summary>
        /// Sends a batch of messages using the batch send method
        /// </summary>
        Task SendAsync<T>(IList<T> messages, CancellationToken cancellationToken = default) where T : IServiceBusCommand;

        /// <summary>
        /// Sends a topic message immediately
        /// </summary>
        Task PublishEventAsync<T>(T message, CancellationToken cancellationToken = default) where T : IServiceBusEvent;
    }

    public class ServiceBus : IServiceBus
    {
        private readonly IServiceBusConfiguration _configuration;
        private IMessageAttachmentProvider _attachmentProvider;

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

        public async Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : IServiceBusCommand
        {
            var client = await ClientFactory.GetSenderClient<T>().ConfigureAwait(false);
            var sbMessage = await CreateMessageAsync(message).ConfigureAwait(false);

            await SendAsync(client, sbMessage, cancellationToken).ConfigureAwait(false);
        }

        public async Task SendAsync<T>(IList<T> messages, CancellationToken cancellationToken = default) where T : IServiceBusCommand
        {
            var client = await ClientFactory.GetSenderClient<T>().ConfigureAwait(false);
            if (!messages.Any()) return;
            var sbMessages = new List<ServiceBusMessage>();
            foreach (var message in messages)
            {
                sbMessages.Add(await CreateMessageAsync(message).ConfigureAwait(false));
            }

            await SendAsync(client, sbMessages, cancellationToken).ConfigureAwait(false);
        }

        public async Task ScheduleAsync<T>(T message, TimeSpan span, CancellationToken cancellationToken = default) where T : IServiceBusCommand
        {
            var client = await ClientFactory.GetSenderClient<T>().ConfigureAwait(false);
            var sbMessage = await CreateMessageAsync(message).ConfigureAwait(false);
            sbMessage.ScheduledEnqueueTime = DateTime.UtcNow.Add(span);

            await SendAsync(client, sbMessage, cancellationToken).ConfigureAwait(false);
        }

        public async Task PublishEventAsync<T>(T message, CancellationToken cancellationToken = default) where T : IServiceBusEvent
        {
            var client = await ClientFactory.GetSenderClient<T>().ConfigureAwait(false);
            var brokeredMessage = await CreateMessageAsync(message).ConfigureAwait(false);

            await SendAsync(client, brokeredMessage, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendAsync(ServiceBusSender client, ServiceBusMessage message, CancellationToken cancellationToken)
        {
            await client.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendAsync(ServiceBusSender client, IList<ServiceBusMessage> messages, CancellationToken cancellationToken)
        {
            await client.SendMessagesAsync(messages, cancellationToken).ConfigureAwait(false);
        }

        private async Task<ServiceBusMessage> CreateMessageAsync<T>(T body) where T : IMessage
        {
            var message = new ServiceBusMessage(_configuration.MessageSerializer.Serialize(body))
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
                    message.ApplicationProperties[AttachmentUtility.AttachmentKey] = string.Join(",", attachmentIds);
                }
            }

            return message;
        }
    }
}