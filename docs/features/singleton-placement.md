# Spreading singleton locks over hosts

[Singleton processors](singleton-processing.md) run on one instance at a time, guarded by a lock per
processor. By default every lock is an independent race, and the instance that starts first wins all
of them and keeps them for as long as it lives. Instances that start later, for example during a
rolling deploy or a scale-out, hold nothing, and the whole singleton workload runs on one host while
the others sit idle. If that workload is heavy, such as a batch re-index, the host that hoards it is
also the one that runs out of memory.

Singleton placement spreads the locks over the live instances. It is opt in: nothing changes unless a
placement is registered.

## Enabling it

Placement needs the blob lock manager, because it uses blob leases to tell which instances are alive:

```csharp
services
    .UseBlobStorage(storageConnectionString)
    .UseBlobStorageLockManager()
    .UseBlobStorageSingletonPlacement(options =>
    {
        // Instances only coordinate with instances in the same group.
        options.Group = "my-worker";

        // Say which processors are heavy, so they are kept on different instances.
        options.Weight<RebuildSearchIndexProcessor>(10);
        options.Weight<RebuildRecommendationsProcessor>(10);
    });
```

Every instance in a group must run the same singleton processors. The group defaults to the name of
the entry assembly. Processors without a declared weight count as 1.

## What it does

- With *N* live instances and *M* singleton processors, each instance ends up holding about *M/N* of
  the weight, within a bounded imbalance.
- An instance that joins receives locks without the others restarting.
- When an instance leaves or dies, the others take its locks over.
- Two heavy processors are not placed on the same instance while an empty one exists.

Placement is advisory. The lock itself still decides who may process, so a mistake in placement can
make the distribution worse but can never make two instances process the same singleton. If the
placement cannot read the group, it stops giving advice and every instance competes for the locks as
it did before.

## How it works

### Membership

Each instance keeps a lease on its own member blob, `{lock directory}/_members/{group}/{host id}`, and
lists the member blobs of its group once per refresh interval. An instance is alive while its blob is
leased. The storage service decides that, so the clocks of the instances never need to agree. The
member blob holds the processors the instance can run and their weights. The host id is the machine
name (the pod name in Kubernetes) plus a random suffix per process start, so a restarted process is a
new member.

### Assignment

Every instance computes the same assignment from the same members, so no coordinator is needed. Locks
are taken heaviest first. For each lock the instances are ranked by a stable hash of the lock id and
the instance id (rendezvous hashing), and the first instance in the ranking that still has room gets
it. An instance has room while it holds less than an average share, and the cap on what it may hold
is its fair share of the weight that is left, plus some slack (`Epsilon`).

- Rendezvous hashing keeps a lock where it is when other instances join or leave: only about 1/N of
  the locks move.
- The cap stops one instance ending up with a disproportionate share, and keeps heavy locks apart.
  A lock heavier than the cap still gets placed, on an instance that holds nothing yet.
- A lock is only assigned to an instance that can run it.

`Epsilon` trades balance against movement. Measured over many random sets of 11 instances with 17
light processors and two of weight 10:

| `Epsilon` | Instances without work | Locks moved needlessly when an instance leaves | Locks moved when an instance joins |
| --- | --- | --- | --- |
| 0 | 0.0 | 3.6 of 19 | 3.2 |
| 0.25 (default) | 1.0 | 0.8 of 19 | 2.3 |

The two heavy locks never shared an instance. A cap bounds the largest load, not the smallest, so
with `Epsilon` above 0 one instance of 11 typically has no work. Every move is a hand-over, which
costs a drain and possibly a cancellation, so the default favours stability.

### Taking a lock

- An instance that is the preferred holder takes the lock as soon as it is free, as before.
- An instance that is not the preferred holder leaves the lock alone. It takes it only if it has seen
  the lock free for a whole `TakeoverGrace`, which covers a preferred instance that is alive but not
  taking the lock. Seeing a lock free needs a lock manager that can inspect a lock without taking it
  (`ISingletonLockInspector`, which the blob lock manager implements). The grace is measured from when
  the lock was first seen free, not from when the instance started waiting.

### Handing a lock over

An instance that holds a lock it is not the preferred holder of hands it over once the preferred
instance has been a member for `StabilityWindow` and the lock has been held for `HandoffInterval`.
Both exist so that locks do not move to an instance that is about to be replaced, which is the normal
state during a rolling deploy, and do not move back and forth.

A hand-over stops fetching new messages, lets the message being processed finish for up to
`DrainTimeout`, then cancels the handler, waits briefly for it to unwind and releases the lock. Every
handler must therefore tolerate cancellation, as it already has to on shutdown.

Stopping the fetch without cancelling the running message needs support from the receiver
(`IDrainableChannelReceiver`). The Azure Storage queue and Azure Service Bus receivers have it.
Receivers without it, today Redis, PostgreSQL and NATS, cancel the running message straight away.

A cancelled message is abandoned and delivered again. The delivery counts against the processor's
`DeadLetterDeliveryLimit`, because the transport counts every delivery and KnightBus cannot take one
back, and a message is dead-lettered when its delivery count exceeds the limit. This only matters if
one message is cancelled repeatedly, which `HandoffInterval` and `DrainTimeout` make unlikely, but a
processor with a limit of 1 or 2 should not take long to process a message.

### Leaving

On shutdown an instance leaves the group first, so the others start counting on its locks while it
drains, and releases its own locks as it always did.

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `Group` | entry assembly name | Instances coordinate only within a group |
| `RefreshInterval` | 20 s | How often an instance reads who else is in the group |
| `StaleAfter` | 3 × `RefreshInterval` | If the group cannot be read for this long, placement stops giving advice |
| `Epsilon` | 0.25 | Slack over the fair share, see above |
| `StabilityWindow` | 2 min | How long an instance must have been a member before locks are handed to it |
| `TakeoverGrace` | 2 min | How long a lock must be seen free before an instance that is not preferred takes it |
| `HandoffInterval` | 10 min | The shortest time a lock is held before it is handed over |
| `DrainTimeout` | 5 min | How long a hand-over waits for the running message before cancelling it |
| `Weight<TProcessor>(n)` | 1 | How heavy a processor is relative to the others |

How quickly a lock moves is bounded by the singleton poll interval as well, see
[singleton processing](singleton-processing.md#what-the-marker-changes) and `ConfigureSingletons`.

## Seeing where the locks are

The blob lease of each lock records the holding instance in its metadata (`HostName`, which is the
machine name, and `AcquiredAtUtc`), so the current distribution can be read from the lock container
without any other tooling. Placement logs when it hands a lock over.

## Alternatives that were considered

| Option | Why not |
| --- | --- |
| Cap the number of locks per instance in a custom lock manager | Needs the number of instances from somewhere, gives no stable ownership, and never hands a lock off, so a new instance gets nothing until someone dies |
| Plain rendezvous hashing | Stable but unbalanced for few locks, and cannot keep heavy locks apart. It is the ranking inside the chosen design |
| Greedy assignment by weight | The best balance, but every membership change reshuffles most locks |
| A leader that computes the assignment | Needs leader election, which is itself a lock, and a leader failure stalls re-assignment |
| Broker sessions as the singleton | Only covers one transport and is a much larger change to the programming model |
| Placement from measured load | Needs a trusted, comparable load signal. Declared weights approximate it |

## Known limits

- Schedules (`IProcessSchedule`) are not placed. A schedule trigger is short and normally only sends a
  command, so the work lands on the message processors, which are placed.
- Only the blob lock manager supports placement.
- Instances that run different sets of singleton processors must be in different groups.
