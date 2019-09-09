using System;
using System.Collections.Generic;
using System.Reflection;

namespace KnightBus.Core
{
    public interface IDependencyInjection
    {
        IDisposable GetScope();
        T GetInstance<T>() where T : class;
        T GetInstance<T>(Type type);
        IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric);
        void RegisterOpenGeneric(Type openGeneric, Assembly assembly);
    }
}