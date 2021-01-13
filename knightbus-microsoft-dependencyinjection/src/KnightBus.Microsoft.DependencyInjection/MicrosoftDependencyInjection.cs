using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Microsoft.DependencyInjection
{
    public class MicrosoftDependencyInjection : IIsolatedDependencyInjection
    {
        private IServiceProvider _provider;
        private readonly IServiceCollection _serviceCollection;
        private readonly IServiceScope _scope;
        private readonly ServiceProviderOptions _options;

        public MicrosoftDependencyInjection(IServiceCollection serviceCollection, IServiceScope scope = null, ServiceProviderOptions options = null)
        {
            _serviceCollection = serviceCollection;
            _scope = scope;
            _options = options ?? new ServiceProviderOptions {ValidateScopes = true};
        }

        private MicrosoftDependencyInjection(IServiceProvider provider, IServiceCollection serviceCollection, IServiceScope scope = null)
        {
            _provider = provider;
            _serviceCollection = serviceCollection;
            _scope = scope;
        }

        public IDependencyInjection GetScope()
        {
            if(_provider == null) throw new DependencyInjectionNotBuiltException();
            var scope = _provider.CreateScope();
            return new MicrosoftDependencyInjection(scope.ServiceProvider, _serviceCollection, scope);
        }

        public T GetInstance<T>() where T : class
        {
            if(_provider == null) throw new DependencyInjectionNotBuiltException();
            return _provider.GetService<T>();
        }

        public T GetInstance<T>(Type type)
        {
            if(_provider == null) throw new DependencyInjectionNotBuiltException();
            return (T)_provider.GetService(type);
        }

        public IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric)
        {
            var allTypes = _serviceCollection.Select(x => x.ImplementationType).Where(t => t != null).Distinct();

            return ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(openGeneric, allTypes).Distinct();
        }

        public void RegisterOpenGeneric(Type openGeneric, Assembly assembly)
        {
            foreach (var openImpl in ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(openGeneric, assembly))
                foreach (var openInterface in ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(openImpl, openGeneric))
                    _serviceCollection.AddScoped(openInterface, openImpl);
        }

        public void Build()
        {
            if (_provider == null)
                _provider = _serviceCollection.BuildServiceProvider(_options);
        }

        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}
