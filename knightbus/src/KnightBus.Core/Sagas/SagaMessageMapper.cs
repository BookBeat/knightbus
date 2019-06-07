using System;
using System.Collections.Generic;
using KnightBus.Messages;

namespace KnightBus.Core.Sagas
{
    public interface ISagaMessageMapper
    {
        void MapMessage<TMessage>(Func<TMessage, string> mapping) where TMessage : IMessage;
        void MapStartMessage<TMessage>(Func<TMessage, string> mapping) where TMessage : IMessage;
        Func<TMessage, string> GetMapping<TMessage>() where TMessage : IMessage;
        bool IsStartMessage(Type type);
    }

    internal class SagaMessageMapper : ISagaMessageMapper
    {
        private readonly Dictionary<Type, object> _mappings = new Dictionary<Type, object>();
        private readonly HashSet<Type> _startMessages = new HashSet<Type>();
        public void MapMessage<TMessage>(Func<TMessage, string> mapping) where TMessage : IMessage
        {
            if (!_mappings.ContainsKey(typeof(TMessage)))
            {
                _mappings.Add(typeof(TMessage), mapping);
            }
        }

        public void MapStartMessage<TMessage>(Func<TMessage, string> mapping) where TMessage : IMessage
        {
            MapMessage(mapping);
            _startMessages.Add(typeof(TMessage));
        }

        public Func<TMessage, string> GetMapping<TMessage>() where TMessage : IMessage
        {
            try
            {
                return _mappings[typeof(TMessage)] as Func<TMessage, string>;
            }
            catch (KeyNotFoundException)
            {
                throw new SagaMessageMappingNotFoundException(typeof(TMessage));
            }
        }

        public bool IsStartMessage(Type type)
        {
            return _startMessages.Contains(type);
        }
    }

    public class SagaMessageMappingNotFoundException : Exception
    {
        public SagaMessageMappingNotFoundException(Type type) : base($"No mapping found for message {type.FullName}")
        { }
    }
}