using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core
{
    /// <summary>
    /// Middleware for message processing
    /// </summary>
    public interface IMessageProcessorMiddleware
    {
        Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage;
    }

    public struct MiddlewarePipelineInformation
    {
        public MiddlewarePipelineInformation(Type messageType, Type subscriptionType, Type settingsType, Type processorType, IHostConfiguration hostConfiguration)
        {
            MessageType = messageType;
            SubscriptionType = subscriptionType;
            SettingsType = settingsType;
            ProcessorType = processorType;
            HostConfiguration = hostConfiguration;
        }
        public Type MessageType { get; }
        public Type SubscriptionType { get; }
        public Type SettingsType { get; }
        public Type ProcessorType { get; }
        public IHostConfiguration HostConfiguration { get; }
    }
}