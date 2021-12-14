using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using NATS.Client;

namespace KnightBus.Nats
{
    internal class NatsBusMessageStateHandler<T> : IMessageStateHandler<T> where T : class, IMessage
    {
        private readonly T _message;
        private readonly Msg _processMessage;
        private readonly IMessageSerializer _serializer;

        public NatsBusMessageStateHandler(Msg processMessage, IMessageSerializer serializer, int deadLetterDeliveryLimit, IDependencyInjection messageScope)
        {
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            MessageScope = messageScope;
            _processMessage = processMessage;
            _serializer = serializer;
            _message = serializer.Deserialize<T>(processMessage.Data.AsSpan());
        }

        public int DeliveryCount { get; } = 1;
        public int DeadLetterDeliveryLimit { get; }

        public IDictionary<string, string> MessageProperties => _processMessage.Header.Keys.Cast<string>().ToDictionary(key => key, key => _processMessage.Header[key]);


        public Task CompleteAsync()
        {
            _processMessage.Respond(null);
            return Task.CompletedTask;
        }

        public Task ReplyAsync<TReply>(TReply reply)
        {
            _processMessage.Respond(_serializer.Serialize(reply));
            return Task.CompletedTask;
        }

        public Task AbandonByErrorAsync(Exception e)
        {
            return Task.CompletedTask;
        }

        public Task DeadLetterAsync(int deadLetterLimit)
        {
            return Task.CompletedTask;
        }
        
        public Task<T> GetMessageAsync()
        {
            return Task.FromResult(_message);
        }

        public IDependencyInjection MessageScope { get; set; }
    }
}