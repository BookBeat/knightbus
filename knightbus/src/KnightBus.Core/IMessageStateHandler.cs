using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core;

/// <summary>
/// Changes the state of a message on the specific transport
/// </summary>
public interface IMessageStateHandler<T> where T : class, IMessage
{
    /// <summary>
    /// Number of times the messages has been picked up by a receiver
    /// </summary>
    int DeliveryCount { get; }
    /// <summary>
    /// Dead letter message after this limit has been reached
    /// </summary>
    int DeadLetterDeliveryLimit { get; }
    /// <summary>
    /// Key Value store of message properties
    /// </summary>
    IDictionary<string, string> MessageProperties { get; }
    /// <summary>
    /// Marks the message as successfully completed
    /// </summary>
    Task CompleteAsync();
    /// <summary>
    /// Replies to the caller
    /// </summary>
    Task ReplyAsync<TReply>(TReply reply);
    /// <summary>
    /// Mark the message as failed and available for pickup by another receiver
    /// </summary>
    Task AbandonByErrorAsync(Exception e);
    /// <summary>
    /// Move the message to dead letter queue
    /// </summary>
    Task DeadLetterAsync(int deadLetterLimit);
    /// <summary>
    /// Retrieves the message
    /// </summary>
    T GetMessage();
    /// <summary>
    /// Gets the message DI scope
    /// </summary>
    IDependencyInjection MessageScope { get; set; }
}
