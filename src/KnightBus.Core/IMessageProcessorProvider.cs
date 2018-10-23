using System;
using System.Collections.Generic;
using KnightBus.Messages;

namespace KnightBus.Core
{
    /// <summary>
    /// Enables IoC when resolving specific MessageProcessors
    /// </summary>
    public interface IMessageProcessorProvider
    {
        IProcessMessage<T> GetProcessor<T>(Type type) where T : IMessage;
        IEnumerable<Type> ListAllProcessors();
    }
}