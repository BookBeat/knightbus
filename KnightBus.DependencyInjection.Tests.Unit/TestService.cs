using System;
using System.ComponentModel;
using FluentAssertions;
using Moq;
using KnightBus.Microsoft.DependencyInjection;


namespace KnightBus.DependencyInjection.Tests.Unit
{
    public interface ITestService
    {
        Guid GetScopeIdentifier();
    }

    public class TestService : ITestService
    {
        private readonly Guid _scopeId;

        public TestService()
        {
            _scopeId = Guid.NewGuid();
        }
        public Guid GetScopeIdentifier()
        {
            return _scopeId;
        }

    }
}