using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusMessageStateHandler<T> : IMessageStateHandler<T> where T : class, IMessage
    {
        private readonly IReceiverClient _client;
        private readonly Message _sbMessage;
        private readonly IMessageAttachmentProvider _attachmentProvider;
        private readonly T _message;

        public ServiceBusMessageStateHandler(IReceiverClient client, Message sbMessage, IMessageSerializer serializer, IMessageAttachmentProvider attachmentProvider, int deadLetterDeliveryLimit)
        {
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            _client = client;
            _sbMessage = sbMessage;
            _attachmentProvider = attachmentProvider;
            _message = serializer.Deserialize<T>(Encoding.UTF8.GetString(_sbMessage.Body));
        }

        public int DeliveryCount => _sbMessage.SystemProperties.DeliveryCount;
        public int DeadLetterDeliveryLimit { get; }
        public IDictionary<string, string> MessageProperties => _sbMessage.UserProperties?.Where(kvp => kvp.Value is string).ToDictionary(k => k.Key, k => k.Value.ToString()) ?? new Dictionary<string, string>();


        public async Task CompleteAsync()
        {
            if (typeof(ICommandWithAttachment).IsAssignableFrom(typeof(T)))
            {
                var queueName = AutoMessageMapper.GetQueueName<T>();
                foreach (var attachmentId in AttachmentUtility.GetAttachmentIds(MessageProperties))
                {
                    await _attachmentProvider.DeleteAttachmentAsync(queueName, attachmentId).ConfigureAwait(false);
                }
            }
            await _client.CompleteAsync(_sbMessage.SystemProperties.LockToken).ConfigureAwait(false);
        }

        public async Task AbandonByErrorAsync(Exception e)
        {
            await _sbMessage.AbandonByErrorAsync(_client, e).ConfigureAwait(false);
        }

        public async Task DeadLetterAsync(int deadLetterLimit)
        {
            await _sbMessage.DeadLetterByDeliveryLimitAsync(_client, deadLetterLimit).ConfigureAwait(false);
        }
        public Task<T> GetMessageAsync()
        {
            return Task.FromResult(_message);
        }
    }
}