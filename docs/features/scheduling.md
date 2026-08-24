# Scheduling

KnightBus has two unrelated features that both involve time, and it is worth being clear about which
one you need:

- **[Recurring schedules](#recurring-schedules)** — cron-triggered jobs that run on a timetable. No
  message is involved.
- **[Deferred messages](#deferred-messages)** — an ordinary message that becomes visible to its
  consumer later.

## Recurring schedules

Install `KnightBus.Schedule`. A schedule is a type describing *when*, and a processor describing
*what*:

```csharp
public class EveryMinute : ISchedule
{
    public string CronExpression => "0 * * ? * *";
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}

public class CleanupJob : IProcessSchedule<EveryMinute>
{
    private readonly ILogger<CleanupJob> _logger;

    public CleanupJob(ILogger<CleanupJob> logger) => _logger = logger;

    public Task ProcessAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up");
        return Task.CompletedTask;
    }
}
```

`ProcessAsync` takes no message — the schedule *is* the trigger.

One processor can serve several schedules, which is how you give the same work two timetables:

```csharp
public class CleanupJob : IProcessSchedule<EveryMinute>, IProcessSchedule<EveryMidnight>
{
    public Task ProcessAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### Registration

```csharp
services
    .UseBlobStorage(storageConnectionString)
    .UseBlobStorageLockManager()   // required — see below
    .UseScheduling()
    .RegisterSchedules();
```

`RegisterSchedules()` scans the calling assembly; pass an `Assembly` to scan another.

!!! warning "Scheduling requires a distributed lock manager"
    Every instance runs its own scheduler and fires its own triggers. What makes a job run **once
    across the cluster** is a distributed lock taken before each execution — so `UseScheduling()`
    needs an `ISingletonLockManager` registered (see
    [singleton processing](singleton-processing.md#supplying-a-lock-manager)), even if you never mark
    a processor `ISingletonProcessor`. Without one, the host fails at startup.

    The lock is taken per schedule/processor pair for 60 seconds and renewed every 19 seconds while
    the job runs. An instance that cannot acquire it silently skips that occurrence.

    After a run finishes the lock is deliberately **not** released — it is left to expire. This stops
    a fast-completing job from being run again in the same window by another node or a
    slightly-drifted clock. The practical consequence is that **a schedule cannot effectively fire
    more often than about once a minute**; for anything faster, use a long-running processor rather
    than a schedule.

If a job throws, the exception is logged and swallowed. There is no retry — the next occurrence is
simply the next one on the timetable. If a job loses its lock mid-run, its cancellation token is
cancelled.

### Cron expressions

Expressions use [Quartz syntax](https://www.quartz-scheduler.org/documentation/quartz-2.3.0/tutorials/crontrigger.html),
which starts with a **seconds** field:

```
┌───────────── seconds (0-59)
│ ┌─────────── minutes (0-59)
│ │ ┌───────── hours (0-23)
│ │ │ ┌─────── day of month (1-31, or ?)
│ │ │ │ ┌───── month (1-12)
│ │ │ │ │ ┌─── day of week (1-7 or SUN-SAT, or ?)
│ │ │ │ │ │
0 * * ? * *
```

| Expression | Meaning |
| --- | --- |
| `0 * * ? * *` | Every minute |
| `0 0 * ? * *` | Every hour |
| `0 0 3 ? * *` | Every day at 03:00 |
| `0 0 3 ? * MON` | Every Monday at 03:00 |
| `0 */15 * ? * *` | Every 15 minutes |

Expressions are validated at **startup**, so an invalid one fails the host immediately rather than
silently never firing. Note that validation aborts the whole registration loop, so schedules that
would have been registered after the invalid one are not registered either — fix the reported
expression and restart. `TimeZone` is applied to the expression, so a daily job in a DST-observing
zone stays at the same local hour year round.

A schedule type is instantiated by reflection to read its cron expression, so it needs a public
parameterless constructor.

### Shutdown

The scheduler is a stoppable plugin: on shutdown it stops triggering new occurrences and waits for
running jobs to finish, within the [shutdown grace period](../concepts/host.md#shutdown).

## Deferred messages

Deferred delivery is a property of the transport client, and only some transports have it.

| Transport | Deferred delivery | Cancellation |
| --- | --- | --- |
| Azure Service Bus | `ScheduleAsync` | `CancelScheduledAsync` |
| Azure Storage Queues | `ScheduleAsync` | — |
| PostgreSQL | `ScheduleAsync` | — |
| Redis | — | — |
| NATS | — | — |

```csharp
// Deliver in one hour
await storageBus.ScheduleAsync(new SendReminder { UserId = userId }, TimeSpan.FromHours(1));
```

The delay is always relative.

### Cancelling on Service Bus

Service Bus returns a sequence number you can use to cancel the message before it is delivered:

```csharp
var sequenceNumber = await serviceBus.ScheduleAsync(
    new SendReminder { UserId = userId },
    TimeSpan.FromHours(1)
);

// Later — the user acted, so the reminder is no longer needed
await serviceBus.CancelScheduledAsync<SendReminder>(sequenceNumber);
```

Store the sequence number if you might need to cancel; it is the only handle on the scheduled message.
The batch overload returns one sequence number per message, in the order the messages were passed, and
either schedules all of them or none.

Scheduled messages can be inspected before their delivery time with `PeekScheduled` on the
[management API](management.md).

### Choosing between them

Deferred messages are the right tool for a delay attached to one piece of work — a reminder, a
timeout, a retry with backoff. Recurring schedules are for work that happens on a timetable
regardless of any particular message.

For a delay on a transport without deferred delivery, either send over a transport that has it, or
model the wait as a [saga](sagas.md) with a `TimeToLive`.

## See also

- [Singleton processing](singleton-processing.md) — the lock manager schedules depend on.
- [Management API](management.md) — inspecting scheduled messages.
- [Host and configuration](../concepts/host.md#plugins) — how plugins are hosted.
