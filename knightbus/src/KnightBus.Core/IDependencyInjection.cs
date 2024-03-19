using System;
using System.Collections.Generic;

namespace KnightBus.Core;

public interface IDependencyInjection : IDisposable
{
    IDependencyInjection GetScope();
    T GetInstance<T>() where T : class;
    T GetInstance<T>(Type type);
    IEnumerable<T> GetInstances<T>();
    IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric);
}
