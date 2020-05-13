using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

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

    public interface ICountable
    {
        void Count();
    }

    public class TestMessage : ICommand
    {

    }

    public class TestMessageSettings : IProcessingSettings
    {
        public int MaxConcurrentCalls { get; set; } = 1;
        public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
        public int DeadLetterDeliveryLimit { get; set; } = 1;
        public int PrefetchCount { get; set; }
    }


    public class TestCommandHandler : IProcessCommand<TestMessage, TestMessageSettings>
    {
        private readonly ICountable _countable;

        public TestCommandHandler(ICountable countable)
        {
            _countable = countable;
        }
        public Task ProcessAsync(TestMessage message, CancellationToken cancellationToken)
        {
            _countable.Count();
            return Task.CompletedTask;
        }
    }
}