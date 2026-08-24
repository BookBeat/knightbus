# Errors and dead-lettering

KnightBus never lets an exception escape the pipeline. When a handler throws, the message is marked
failed and the transport redelivers it; once it has failed enough times it is moved out of the way so
it stops blocking the queue.

## The retry loop

1. Your handler throws.
2. `ErrorHandlingMiddleware` catches the exception and logs it.
3. The message is abandoned, making it available for redelivery.
4. The transport redelivers it, with `DeliveryCount` incremented.
5. Once `DeliveryCount` exceeds `DeadLetterDeliveryLimit`, the message is dead-lettered instead of
   being handed to your handler again.

There is no built-in delay or backoff between attempts — redelivery timing belongs to the transport.
If you need backoff, either configure it on the transport or catch the error yourself and re-send the
message with a delay using [deferred delivery](scheduling.md#deferred-messages).

## Dead-lettering

`DeadLetterDeliveryLimit` on your [processing settings](../concepts/processors.md#processing-settings)
controls how many attempts a message gets:

```csharp
public class DefaultSettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 10;
    public int PrefetchCount => 50;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
    public int DeadLetterDeliveryLimit => 3;   // handled on attempts 1-3
}
```

The comparison is strictly greater-than: with a limit of 3, your handler runs on deliveries 1, 2 and
3, and delivery 4 dead-letters the message without invoking the handler at all.

!!! warning "The transport's own limit wins"
    Most transports enforce their own maximum delivery count. KnightBus' limit only has an effect if
    it is **lower** than the queue's — otherwise the transport dead-letters the message first, and
    the [`IProcessBeforeDeadLetter<T>`](#hooking-into-dead-lettering) hook never runs.

## Hooking into dead-lettering

To do something at the moment a message is about to be given up on — raise an alert, write an audit
row, notify a user — implement `IProcessBeforeDeadLetter<T>` on the **same class** that handles the
message:

```csharp
public class OrderProcessor
    : IProcessCommand<PlaceOrder, DefaultSettings>,
        IProcessBeforeDeadLetter<PlaceOrder>
{
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(ILogger<OrderProcessor> logger) => _logger = logger;

    public Task ProcessAsync(PlaceOrder message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom");

    public Task BeforeDeadLetterAsync(PlaceOrder message, CancellationToken cancellationToken)
    {
        _logger.LogError("Giving up on order {OrderId}", message.OrderId);
        return Task.CompletedTask;
    }
}
```

The hook fires once, on the delivery that dead-letters the message, immediately before it is moved.
Two properties are worth knowing:

- **It cannot veto dead-lettering.** The message is dead-lettered whatever the hook does.
- **Exceptions from the hook are swallowed** (logged as an error). A broken hook cannot get a message
  stuck.

Implement it on the same class, closed over the same message type, as the processor — it is resolved
through the processor interface, so a hook on a different class is never found.

## Message locks

While your handler runs, the transport holds a lock on the message so nobody else picks it up.
`MessageLockTimeout` is both that lock duration and the deadline on the `CancellationToken` your
handler receives.

If processing outlives the lock, the message becomes visible again and is processed a second time
while the first attempt is still running. This is the most common cause of accidental duplicate
processing, and the reason to honour the cancellation token.

For genuinely long work, prefer a short renewed lock over one enormous timeout — see
[extending the lock](../concepts/processors.md#long-running-work-extending-the-lock). That mechanism
needs `ExtendMessageLockDurationMiddleware` registered manually and only functions on Azure Storage
Queues, the sole transport that can change a lock mid-flight.

## Where dead letters go

The destination is transport-specific:

| Transport | Dead letter location |
| --- | --- |
| Azure Service Bus | The queue's or subscription's built-in dead letter sub-queue. |
| Azure Storage Queues | A separate queue named `{queue}-dl`. |
| PostgreSQL | Table `knightbus.dlq_{queue}` (or `dlq_{topic}_{subscription}`). |
| Redis | The list `{queue}:deadletter`. |
| NATS | No dead letter support. |

The [management API](management.md) reads and requeues dead letters uniformly across transports, so
you rarely need to know the physical layout:

```csharp
var deadLetters = await queueManager.PeekDeadLetter(queueName, count, cancellationToken);
await queueManager.MoveDeadLetters(queueName, count, cancellationToken);
```

## Poison messages

A message that always fails — bad data, a contract change — burns through its delivery attempts and
lands in the dead letter queue. That is the intended outcome: the queue keeps moving and the bad
message is preserved for inspection.

What to watch for:

- **Alert on dead letter depth.** A message arriving there is a signal that needs a human.
- **Keep `DeadLetterDeliveryLimit` low** for messages that fail deterministically. Retrying a
  malformed payload 50 times only delays the inevitable.
- **Fix forward, then requeue.** Deploy the fix, then `MoveDeadLetters` to replay them.

## See also

- [Message processors](../concepts/processors.md) — the settings involved.
- [Management API](management.md) — inspecting and requeueing dead letters.
- [Monitoring](../monitoring.md) — surfacing failures.
