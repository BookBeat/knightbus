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
        private readonly Msg _msg;
        private readonly IMessageSerializer _serializer;

        public NatsBusMessageStateHandler(Msg msg, IMessageSerializer serializer, int deadLetterDeliveryLimit, IDependencyInjection messageScope)
        {
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            MessageScope = messageScope;
            _msg = msg;
            _serializer = serializer;
            _message = serializer.Deserialize<T>(msg.Data.AsSpan());
        }

        public int DeliveryCount => 1;
        public int DeadLetterDeliveryLimit { get; }

        public IDictionary<string, string> MessageProperties => _msg.Header.Keys.Cast<string>().ToDictionary(key => key, key => _msg.Header[key]);


        public Task CompleteAsync()
        {
            TryReply(MsgConstants.Completed);
            return Task.CompletedTask;
        }

        public Task ReplyAsync<TReply>(TReply reply)
        {
            _msg.Respond(_serializer.Serialize(reply));
            return Task.CompletedTask;
        }

        public Task AbandonByErrorAsync(Exception e)
        {
            TryReply(MsgConstants.Error);
            return Task.CompletedTask;
        }

        public Task DeadLetterAsync(int deadLetterLimit)
        {
            return Task.CompletedTask;
        }
        
        public T GetMessage()
        {
            return _message;
        }

        public IDependencyInjection MessageScope { get; set; }

        private void TryReply(string status)
        {
            if (!string.IsNullOrEmpty(_msg.Reply))
            {
                var msg = new Msg(_msg.Reply);
                msg.Header.Add(MsgConstants.HeaderName, status);
                try
                {
                    _msg.ArrivalSubscription.Connection.Publish(msg);
                }
                catch (NATSException)
                {
                    //Replies are best effort
                }
            }
        }
    }

    internal class MsgConstants
    {
        public const string HeaderName = "kb";
        public const string Completed = "ok";
        public const string Error = "err";
    }
}