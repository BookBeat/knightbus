using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core
{
    /// <summary>
    /// Changes the state of a message on the specific transport
    /// </summary>
    public interface IMessageStateHandler<T> where T : class, IMessage
    {
        int DeliveryCount { get; }
        int DeadLetterDeliveryLimit { get; }
        IDictionary<string, string> MessageProperties { get; }
        Task CompleteAsync();
        Task AbandonByErrorAsync(Exception e);
        Task DeadLetterAsync(int deadLetterLimit);
        Task<T> GetMessageAsync();
    }
}