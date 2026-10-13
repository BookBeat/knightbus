# Spreading singleton locks over hosts

Singleton placement spreads the [singleton processor](singleton-processing.md) locks over the
running instances, so that one instance does not end up running all of them.

!!! note "Enabled by default"
    Placement is enabled by default when you use the Azure Blob Storage lock manager
    (`UseBlobStorageLockManager()`). No configuration is needed, and it can be
    [switched off](#opting-out).

A singleton processor runs on one instance at a time, guarded by a lock per processor. Without
placement every lock is an independent race, and the instance that starts first wins all of them and
keeps them for as long as it lives. Instances that start later, for example during a rolling deploy
or a scale-out, hold nothing, and the whole singleton workload runs on one host while the others sit
idle. If that workload is heavy, such as a batch re-index, the host that hoards it is also the one
that runs out of memory.

## Setup

Placement uses blob leases to tell which instances are alive, so there is nothing to set up beyond
the blob lock manager:

```csharp
services
    .UseBlobStorage(storageConnectionString)
    .UseBlobStorageLockManager();
```

- It applies to singleton processors on any transport, because the locks are always blobs. Cron
  schedule locks are not placed.
- Instances that use the same lock scheme (container and directory) coordinate with each other, so
  they should run the same singleton processors. A host without singleton processors does not take
  part.
- Besides the lock blobs, each instance creates, leases, lists and deletes small member blobs under
  `{lock directory}/_members/` in the lock container. A storage role that is limited to the lock
  blobs needs to allow that too.

### Opting out

To run without placement, where the first instance to start takes every lock, remove the placement
registration after the lock manager has been registered:

```csharp
using KnightBus.Core.Singleton;
using Microsoft.Extensions.DependencyInjection.Extensions;

services
    .UseBlobStorage(storageConnectionString)
    .UseBlobStorageLockManager()
    .RemoveAll<ISingletonPlacement>();
```

## Declaring a heavy processor

Hash spread alone does not keep two heavy processors apart. Say how heavy a processor is by
implementing `ISingletonWeight` on its processing settings. Processors without it count as 1, and
the weight must be at least 1:

```csharp
public class RebuildSearchIndexSettings : IProcessingSettings, ISingletonWeight
{
    public int MaxConcurrentCalls => 1;
    public int PrefetchCount => 0;
    public TimeSpan MessageLockTimeout => TimeSpan.FromHours(2);
    public int DeadLetterDeliveryLimit => 3;

    // Counts as ten light processors when the locks are spread out
    public int Weight => 10;
}
```

## What it does

- With *N* live instances and *M* singleton processors, each instance ends up holding about *M/N* of
  the weight, within a bounded imbalance.
- An instance that joins receives locks without the others restarting.
- When an instance leaves or dies, the others take its locks over.
- Two heavy processors are not placed on the same instance while an empty one exists.

Placement is advisory. The lock itself decides who may process, so a mistake in placement can
make the distribution worse but can never make two instances process the same singleton. If the
placement cannot read the member list, it stops giving advice and every instance competes for the
locks as it does without placement.

## Seeing what it does

- Each lock blob records who holds it in its metadata: `LockId`, `HostName` (the machine name, the
  pod name in Kubernetes) and `AcquiredAtUtc`. Listing the lock blobs with their metadata shows the
  current distribution.
- A holder that gives a lock up logs `Singleton Processor with name {ProcessorName} hands its lock
  over to {Host}` at Information.

## How it works

### Membership

Each instance keeps a lease on its own member blob, `{lock directory}/_members/{host id}`, and lists
the member blobs in that directory every 20 seconds. An instance is alive while its blob is leased.
The storage service decides that, so the clocks of the instances never need to agree. The member
blob holds the processors the instance can run and their weights. The host id is the machine name
(the pod name in Kubernetes) plus a random suffix per process start, so a restarted process is a new
member.

### Assignment

Every instance computes the same assignment from the same members, so no coordinator is needed.
Locks are taken heaviest first. For each lock the instances are ranked by a stable hash of the lock
id and the instance id (rendezvous hashing), and the first instance in the ranking that still has
room gets it. An instance has room while it holds less than an average share, and the cap on what it
may hold is its fair share of the weight that is left, plus some slack (25%).

- Rendezvous hashing keeps a lock where it is when other instances join or leave: only about 1/N of
  the locks move.
- The cap stops one instance ending up with a disproportionate share, and keeps heavy locks apart. A
  lock heavier than the cap still gets placed, on an instance that holds nothing yet.
- A lock is only assigned to an instance that can run it.

The slack trades balance against movement. Measured over many random sets of 11 instances with 17
light processors and two of weight 10:

| Slack | Instances without work | Locks moved needlessly when an instance leaves | Locks moved when an instance joins |
| --- | --- | --- | --- |
| 0% | 0.0 | 3.6 of 19 | 3.2 |
| 25% (used) | 1.0 | 0.8 of 19 | 2.3 |

The two heavy locks never shared an instance. A cap bounds the largest load, not the smallest, so
with any slack one instance of 11 typically has no work. Every move is a hand-over, which costs a
drain and possibly a cancellation, so the slack favours stability.

### Taking a lock

- An instance that is the preferred holder takes the lock as soon as it is free.
- An instance that is not the preferred holder leaves the lock alone. It takes it only if it has
  seen the lock free for a whole 2 minutes, which covers a preferred instance that is alive but not
  taking the lock. Seeing a lock free needs a lock manager that can inspect a lock without taking it
  (`ISingletonLockInspector`, which the blob lock manager implements). The grace is measured from
  when the lock was first seen free, not from when the instance started waiting.

### Handing a lock over

An instance that holds a lock it is not the preferred holder of hands it over once the preferred
instance has been a member for 2 minutes and the lock has been held for 10 minutes. Both exist so
that locks do not move to an instance that is about to be replaced, which is the normal state during
a rolling deploy, and do not move back and forth.

A hand-over stops fetching new messages and lets the message being processed finish, for up to the
processor's `MessageLockTimeout`, which is the longest a message may be in flight anyway. Then it
cancels the handler, waits briefly for it to unwind and releases the lock. A handler that stays
within its message lock is never cancelled by a hand-over, but every handler must still tolerate
cancellation, as it has to on shutdown too. The cancellation is logged like any other cancelled
handler.

Stopping the fetch without cancelling the running message needs support from the receiver
(`IDrainableChannelReceiver`). The Azure Storage queue receiver has it. Receivers without it, today
Redis, PostgreSQL and NATS, cancel the running message straight away.

A cancelled message is abandoned and delivered again. The delivery counts against the processor's
`DeadLetterDeliveryLimit`, because the transport counts every delivery and KnightBus cannot take one
back, and a message is dead-lettered when its delivery count exceeds the limit. This only matters if
one message is cancelled repeatedly, which the 10 minute hold and the wait for the message lock make
unlikely, but a processor with a limit of 1 or 2 should not take long to process a message.

### Leaving

On shutdown an instance leaves the member list first, so the others start counting on its locks
while it drains, and then releases its own locks.

## Fixed behaviour

The timing is fixed and not configurable:

| What | Value |
| --- | --- |
| How often an instance reads the member list | 20 seconds |
| Advice is dropped when the member list could not be read for | 3 refreshes |
| Slack over the fair share | 25% |
| How long an instance must have been a member before locks are handed to it | 2 minutes |
| How long a lock must have been seen free before a non-preferred instance takes it | 2 minutes |
| The shortest time a lock is held before it is handed over | 10 minutes |

How quickly a lock moves is bounded by the singleton poll interval as well, see [singleton
processing](singleton-processing.md#what-the-marker-changes) and `ConfigureSingletons`.
