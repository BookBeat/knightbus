using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host.Tests.Unit;

/// <summary>
/// A blob-lease stand-in that the hosts of a test share: a free lock goes to whoever asks first
/// </summary>
internal sealed class InMemoryLockStore
{
    private readonly ConcurrentDictionary<string, string> _holders = new();
    private int _takes;

    //How many times a lock has been taken, so a lock that moves away and back is noticed too
    public int Takes => Volatile.Read(ref _takes);

    public Dictionary<string, string> Holders() => new(_holders);

    public Dictionary<string, int> HoldersByHost() =>
        _holders.Values.GroupBy(h => h).ToDictionary(g => g.Key, g => g.Count());

    public string? Holder(string lockId) =>
        _holders.TryGetValue(lockId, out var holder) ? holder : null;

    public bool IsLocked(string lockId) => _holders.ContainsKey(lockId);

    //A host outside the test takes the lock, or lets go of it
    public void Take(string lockId, string host) => _holders[lockId] = host;

    public void ReleaseFor(string lockId) => _holders.TryRemove(lockId, out _);

    public bool TryTake(string lockId, string host)
    {
        if (!_holders.TryAdd(lockId, host))
            return false;
        Interlocked.Increment(ref _takes);
        return true;
    }

    public void Release(string lockId, string host) =>
        _holders.TryRemove(new KeyValuePair<string, string>(lockId, host));
}

/// <summary>
/// One host's lock manager on an <see cref="InMemoryLockStore"/>
/// </summary>
internal sealed class InMemoryLockManager : ISingletonLockManager, ISingletonLockInspector
{
    private readonly InMemoryLockStore _store;
    private readonly string _host;
    private int _attempts;
    private int _failedAttempts;
    private int _inspections;
    private int _failuresLeft;

    public InMemoryLockManager(InMemoryLockStore store, string host)
    {
        _store = store;
        _host = host;
    }

    public int Attempts => Volatile.Read(ref _attempts);

    //Attempts that found the lock held
    public int FailedAttempts => Volatile.Read(ref _failedAttempts);

    public int Inspections => Volatile.Read(ref _inspections);

    //The next attempts throw, like a storage error that outlasts the SDK's retries
    public void FailNext(int attempts) => Volatile.Write(ref _failuresLeft, attempts);

    //Renewals fail from now on, like a lease that another host has broken
    public volatile bool FailRenewals;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task<bool> IsLockedAsync(string lockId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _inspections);
        return Task.FromResult(_store.IsLocked(lockId));
    }

    public Task<ISingletonLockHandle?> TryLockAsync(
        string lockId,
        TimeSpan lockPeriod,
        CancellationToken cancellationToken
    )
    {
        Interlocked.Increment(ref _attempts);
        if (Interlocked.Decrement(ref _failuresLeft) >= 0)
            throw new InvalidOperationException("storage unavailable");
        if (_store.TryTake(lockId, _host))
            return Task.FromResult<ISingletonLockHandle?>(new Handle(this, lockId));
        Interlocked.Increment(ref _failedAttempts);
        return Task.FromResult<ISingletonLockHandle?>(null);
    }

    private sealed class Handle(InMemoryLockManager owner, string lockId) : ISingletonLockHandle
    {
        public string LeaseId => owner._host;
        public string LockId => lockId;

        public Task<bool> RenewAsync(ILogger log, CancellationToken cancellationToken) =>
            owner.FailRenewals
                ? throw new InvalidOperationException("lease lost")
                : Task.FromResult(true);

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            owner._store.Release(lockId, owner._host);
            return Task.CompletedTask;
        }
    }
}
