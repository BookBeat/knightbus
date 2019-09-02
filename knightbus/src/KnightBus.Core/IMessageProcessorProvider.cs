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


    public interface IDependencyInjection
    {
        IDisposable GetScope();
        T GetInstance<T>() where T : class;
        object GetInstance(Type type);
        IEnumerable<T> GetAllInstances<T>() where T : class;
        IEnumerable<T> GetAllInstances<T>(Type type);
    }
}