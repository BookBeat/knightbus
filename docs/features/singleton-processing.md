# Singleton processing

A singleton processor handles one message at a time, on one instance, no matter how many instances of
your application are running. It is the answer to "this must not run twice at once" — cache
rebuilds, sequential imports, anything touching a resource that cannot take concurrency.

Add the `ISingletonProcessor` marker to the processor:

```csharp
public class RebuildCacheProcessor
    : IProcessCommand<RebuildCache, DefaultSettings>,
        ISingletonProcessor
{
    public Task ProcessAsync(RebuildCache message, CancellationToken cancellationToken)
    {
        // Never runs concurrently, in this process or any other
        return Task.CompletedTask;
    }
}
```

## Supplying a lock manager

Exclusivity across instances needs a distributed lock, so you must register an
`ISingletonLockManager`. KnightBus ships one implementation, based on Azure Blob Storage leases:

```csharp
services
    .UseBlobStorage(storageConnectionString)
    .UseBlobStorageLockManager();
```

Or register any implementation directly:

```csharp
services.UseSingletonLocks(myLockManager);
```

!!! warning "No lock manager means no startup"
    A processor marked `ISingletonProcessor` with no `ISingletonLockManager` registered fails host
    startup. This is intentional — silently running a singleton processor concurrently would be
    worse.

## What the marker changes

The listener for a singleton processor is wrapped so that it only runs while holding the lock, and
its settings are replaced:

| Setting | Value while singleton |
| --- | --- |
| `MaxConcurrentCalls` | forced to `1` |
| `PrefetchCount` | forced to `0` |
| `MessageLockTimeout` | kept from your settings |
| `DeadLetterDeliveryLimit` | kept from your settings |

Whatever you wrote for the first two is ignored, so there is no point tuning them on a singleton
processor's settings type. If that settings type is shared with non-singleton processors, they are
unaffected — the override applies to the wrapped listener only.

Instances that do not hold the lock poll for it roughly once a minute, so failover after an instance
disappears is not instant. The lock itself is taken for a minute and renewed every 19 seconds while
held.

Events get one lock per subscription, so two subscriptions on the same event each process singly but
independently of one another.

## Behaviour during shutdown

Singleton locks are released only **after** in-flight messages have drained, not when the stop signal
arrives. During a rolling deploy the outgoing instance keeps the lock until it has genuinely finished,
so the incoming instance cannot start processing the same queue concurrently. See
[shutdown](../concepts/host.md#shutdown).

If the process dies without releasing the lock, the lease expires on its own and another instance
picks it up.

## Writing a lock manager

```csharp
public interface ISingletonLockManager
{
    Task<ISingletonLockHandle> TryLockAsync(
        string lockId,
        TimeSpan lockPeriod,
        CancellationToken cancellationToken
    );
    Task InitializeAsync();
}

public interface ISingletonLockHandle
{
    string LeaseId { get; }
    string LockId { get; }
    Task<bool> RenewAsync(ILogger log, CancellationToken cancellationToken);
    Task ReleaseAsync(CancellationToken cancellationToken);
}
```

Two contract details matter:

- **`TryLockAsync` must return `null`** when the lock is held elsewhere. Do not throw — returning
  `null` is how the receiver knows to start polling.
- **`RenewAsync` returns `true`** when the lease was renewed. Renewal is retried up to three times
  with backoff before the lock is considered lost.

The [Schedule example](https://github.com/BookBeat/knightbus/tree/master/knightbus/examples/KnightBus.Examples.Schedule)
contains a minimal in-memory implementation, useful for local development and tests but not for
production — an in-memory lock cannot coordinate across processes.

## Also used by scheduling

[Cron schedules](scheduling.md) take a singleton lock per schedule so a recurring job fires once
across the cluster rather than once per instance. `UseScheduling()` therefore requires an
`ISingletonLockManager` too, even if you never mark a processor `ISingletonProcessor`.

## See also

- [Message processors](../concepts/processors.md) — the settings that singleton mode overrides.
- [Azure Storage Queues](../transports/azure-storage-queues.md) — the Blob lock manager.
