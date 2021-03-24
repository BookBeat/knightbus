using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusMessageStateHandler<T> : IMessageStateHandler<T> where T : class, IMessage
    {
        private readonly ProcessMessageEventArgs _processMessage;
        private readonly ServiceBusReceivedMessage _sbMessage;
        private readonly T _message;

        public ServiceBusMessageStateHandler(ProcessMessageEventArgs processMessage, IMessageSerializer serializer, int deadLetterDeliveryLimit, IDependencyInjection messageScope)
        {
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            MessageScope = messageScope;
            _processMessage = processMessage;
            _sbMessage = processMessage.Message;
            _message = serializer.Deserialize<T>(_sbMessage.Body.ToString());
        }

        public int DeliveryCount => _sbMessage.DeliveryCount;
        public int DeadLetterDeliveryLimit { get; }
        public IDictionary<string, string> MessageProperties => _sbMessage.ApplicationProperties?.Where(kvp => kvp.Value is string).ToDictionary(k => k.Key, k => k.Value.ToString()) ?? new Dictionary<string, string>();


        public async Task CompleteAsync()
        {
            await _processMessage.CompleteMessageAsync(_sbMessage).ConfigureAwait(false);
        }

        public async Task AbandonByErrorAsync(Exception e)
        {
            await _sbMessage.AbandonByErrorAsync(_processMessage, e).ConfigureAwait(false);
        }

        public async Task DeadLetterAsync(int deadLetterLimit)
        {
            await _sbMessage.DeadLetterByDeliveryLimitAsync(_processMessage, deadLetterLimit).ConfigureAwait(false);
        }
        public Task<T> GetMessageAsync()
        {
            return Task.FromResult(_message);
        }

        public IDependencyInjection MessageScope { get; set; }
    }
}