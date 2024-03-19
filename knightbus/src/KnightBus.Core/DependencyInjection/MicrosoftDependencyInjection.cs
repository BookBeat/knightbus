using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Core.DependencyInjection;

public class MicrosoftDependencyInjection : IDependencyInjection
{
    private readonly IServiceProvider _provider;
    private readonly IServiceScope _scope;

    public MicrosoftDependencyInjection(IServiceProvider provider, IServiceScope scope = null)
    {
        _provider = provider;
        _scope = scope;
    }


    public IDependencyInjection GetScope()
    {
        var scope = _provider.CreateScope();
        return new MicrosoftDependencyInjection(scope.ServiceProvider, scope);
    }

    public T GetInstance<T>() where T : class
    {
        return _provider.GetRequiredService<T>();
    }

    public T GetInstance<T>(Type type)
    {
        return (T)_provider.GetRequiredService(type);
    }

    public IEnumerable<T> GetInstances<T>()
    {
        return _provider.GetServices<T>();
    }

    public IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric)
    {
        using var scope = GetScope();
        var allTypes = scope.GetInstances<IGenericProcessor>().Distinct().Select(o => o.GetType());
        return ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(openGeneric, allTypes).Distinct();
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }
}
