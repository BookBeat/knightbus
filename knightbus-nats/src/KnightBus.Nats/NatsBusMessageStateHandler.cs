using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using NATS.Client;

namespace KnightBus.Nats
{
    internal class NatsBusMessageStateHandler<T> : IMessageStateHandler<T> where T : class, IMessage
    {
        private readonly T _message;
        private readonly MsgHandlerEventArgs _processMessage;
        private readonly IMessageSerializer _serializer;

        public NatsBusMessageStateHandler(MsgHandlerEventArgs processMessage, IMessageSerializer serializer, int deadLetterDeliveryLimit, IDependencyInjection messageScope)
        {
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            MessageScope = messageScope;
            _processMessage = processMessage;
            _serializer = serializer;
            _message = serializer.Deserialize<T>(processMessage.Message.Data.AsSpan());
        }

        public int DeliveryCount { get; } = 1;
        public int DeadLetterDeliveryLimit { get; }
        public IDictionary<string, string> MessageProperties { get; }

        public Task CompleteAsync()
        {
            return Task.CompletedTask;
        }

        public Task ReplyAsync<TReply>(TReply reply)
        {
            _processMessage.Message.Respond(_serializer.Serialize(reply));
            return Task.CompletedTask;
        }

        public Task AbandonByErrorAsync(Exception e)
        {
            // await _sbMessage.AbandonByErrorAsync(_processMessage, e).ConfigureAwait(false);
            return Task.CompletedTask;
        }

        public Task DeadLetterAsync(int deadLetterLimit)
        {
            // await _sbMessage.DeadLetterByDeliveryLimitAsync(_processMessage, deadLetterLimit).ConfigureAwait(false);
            return Task.CompletedTask;
        }
        
        public Task<T> GetMessageAsync()
        {
            return Task.FromResult(_message);
        }

        public IDependencyInjection MessageScope { get; set; }
    }
}