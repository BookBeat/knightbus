using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Quartz;

namespace KnightBus.Schedule.Tests.Unit;

public class ScheduleProcessorLockingTests
{
    [Test]
    public async Task WhenProcessorsShareSchedule_ShouldNotBlockEachOtherDueToLock()
    {
        // Arrange
        var lockManager = new TrackingLockManager();
        var processorOne = new TrackingSharedScheduleProcessor();
        var processorTwo = new TrackingSharedSchedule2Processor();
        var executorOne = new JobExecutor<SharedLockSchedule, TrackingSharedScheduleProcessor>(
            Mock.Of<ILogger>(),
            lockManager,
            new SingleProcessorDependencyInjection<SharedLockSchedule>(processorOne)
        );
        var executorTwo = new JobExecutor<SharedLockSchedule, TrackingSharedSchedule2Processor>(
            Mock.Of<ILogger>(),
            lockManager,
            new SingleProcessorDependencyInjection<SharedLockSchedule>(processorTwo)
        );
        var contextOne = Mock.Of<IJobExecutionContext>(ctx =>
            ctx.CancellationToken == CancellationToken.None
        );
        var contextTwo = Mock.Of<IJobExecutionContext>(ctx =>
            ctx.CancellationToken == CancellationToken.None
        );

        // Act
        await Task.WhenAll(executorOne.Execute(contextOne), executorTwo.Execute(contextTwo));

        // Assert
        using var _ = new AssertionScope();
        processorOne.InvocationCount.Should().Be(1);
        processorTwo.InvocationCount.Should().Be(1);
    }

    private sealed class SingleProcessorDependencyInjection<TSchedule> : IDependencyInjection
        where TSchedule : class, ISchedule, new()
    {
        private readonly IProcessSchedule<TSchedule> _processor;

        public SingleProcessorDependencyInjection(IProcessSchedule<TSchedule> processor)
        {
            _processor = processor;
        }

        public IDependencyInjection GetScope()
        {
            return new SingleProcessorDependencyInjection<TSchedule>(_processor);
        }

        public T GetInstance<T>()
            where T : class
        {
            if (_processor.GetType().IsAssignableTo(typeof(T)))
                return (T)_processor;
            throw new InvalidOperationException($"No instance of {typeof(T)} available");
        }

        public T GetInstance<T>(Type type)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> GetInstances<T>()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric)
        {
            throw new NotImplementedException();
        }

        public void Dispose() { }
    }

    // Lock manager that keeps a lock forever once it is taken
    private sealed class TrackingLockManager : ISingletonLockManager
    {
        private readonly ConcurrentDictionary<string, byte> _heldLocks = new();

        public Task<ISingletonLockHandle> TryLockAsync(
            string lockId,
            TimeSpan lockPeriod,
            CancellationToken cancellationToken
        )
        {
            if (_heldLocks.TryAdd(lockId, 0))
            {
                return Task.FromResult<ISingletonLockHandle>(new TrackingLockHandle(lockId));
            }

            return Task.FromResult<ISingletonLockHandle>(null);
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingLockHandle : ISingletonLockHandle
    {
        public TrackingLockHandle(string lockId)
        {
            LockId = lockId;
        }

        public string LeaseId => LockId;
        public string LockId { get; }

        public Task<bool> RenewAsync(ILogger log, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private class TrackingSharedScheduleProcessor : IProcessSchedule<SharedLockSchedule>
    {
        private int _invocationCount;

        public int InvocationCount => _invocationCount;

        public Task ProcessAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _invocationCount);
            return Task.CompletedTask;
        }
    }

    private class TrackingSharedSchedule2Processor : IProcessSchedule<SharedLockSchedule>
    {
        private int _invocationCount;

        public int InvocationCount => _invocationCount;

        public Task ProcessAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _invocationCount);
            return Task.CompletedTask;
        }
    }

    private record SharedLockSchedule : ISchedule
    {
        public string CronExpression => "0 */5 * ? * *";
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
    }
}
