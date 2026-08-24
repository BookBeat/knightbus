# Message processors

A processor is the class that handles a message. You mark it with one of four interfaces, each of
which takes a settings type as its last type parameter.

| Interface | Handles | Returns |
| --- | --- | --- |
| `IProcessCommand<T, TSettings>` | A command | `Task` |
| `IProcessEvent<TTopic, TTopicSubscription, TSettings>` | An event, on one subscription | `Task` |
| `IProcessRequest<TRequest, TResponse, TSettings>` | A request, replying once | `Task<TResponse>` |
| `IProcessStreamRequest<TRequest, TResponse, TSettings>` | A request, replying many times | `IAsyncEnumerable<TResponse>` |

Only these four are valid processor interfaces. Processors are resolved from the DI container with a
**scoped** lifetime — a fresh instance per message — so constructor injection works as it does in a
web request.

## Handling a command

```csharp
public class SampleCommandProcessor : IProcessCommand<SampleCommand, DefaultSettings>
{
    private readonly IOrderRepository _orders;

    public SampleCommandProcessor(IOrderRepository orders) => _orders = orders;

    public Task ProcessAsync(SampleCommand message, CancellationToken cancellationToken) =>
        _orders.SaveAsync(message.OrderId, cancellationToken);
}
```

## Handling an event

An event processor also names the subscription it listens on, which is what separates one listener
from another:

```csharp
public class InvoicingProcessor
    : IProcessEvent<OrderPlaced, InvoicingSubscription, DefaultSettings>
{
    public Task ProcessAsync(OrderPlaced message, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
```

## Handling requests

Request/response is only implemented by the [NATS transport](../transports/nats.md). A request
processor returns a value, which KnightBus sends back to the caller:

```csharp
public class LookupProcessor : IProcessRequest<LookupRequest, LookupReply, DefaultSettings>
{
    public Task<LookupReply> ProcessAsync(LookupRequest message, CancellationToken cancellationToken) =>
        Task.FromResult(new LookupReply { Value = "42" });
}
```

A stream request processor yields many replies, each sent to the caller as it is produced:

```csharp
public class StreamProcessor
    : IProcessStreamRequest<LookupRequest, LookupReply, DefaultSettings>
{
    public async IAsyncEnumerable<LookupReply> ProcessAsync(
        LookupRequest message,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10, cancellationToken);
            yield return new LookupReply { Value = i.ToString() };
        }
    }
}
```

## One class, many messages

A processor class may implement several processor interfaces. This is the normal way to group related
handlers, and it is how [sagas](../features/sagas.md) receive more than one message.

```csharp
public class OrderProcessor
    : IProcessCommand<PlaceOrder, DefaultSettings>,
        IProcessCommand<CancelOrder, DefaultSettings>,
        IProcessEvent<PaymentReceived, OrdersSubscription, DefaultSettings>
{
    public Task ProcessAsync(PlaceOrder message, CancellationToken ct) => Task.CompletedTask;
    public Task ProcessAsync(CancelOrder message, CancellationToken ct) => Task.CompletedTask;
    public Task ProcessAsync(PaymentReceived message, CancellationToken ct) => Task.CompletedTask;
}
```

Each message type still gets its own listener with its own settings, so the concurrency of one does
not affect the others.

## Registering processors

```csharp
services.RegisterProcessors();                              // scans the calling assembly
services.RegisterProcessors(typeof(OrderProcessor).Assembly); // scans a specific assembly
services.RegisterProcessor<OrderProcessor>();               // registers exactly one
```

!!! warning "`RegisterProcessors()` scans the *calling* assembly"
    The parameterless overload uses `Assembly.GetCallingAssembly()`. If you wrap it in a helper
    method in a shared library, it scans that library instead of your application. Pass the assembly
    explicitly when the registration code does not live next to the processors.

A processor that is never registered is never discovered, and its queue is never listened to — with
no error at startup.

## Processing settings

Settings are per listener, supplied as the last type parameter. They must be a class with a
parameterless constructor; KnightBus instantiates them itself.

```csharp
public class DefaultSettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 10;
    public int PrefetchCount => 50;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
    public int DeadLetterDeliveryLimit => 3;
}
```

| Setting | Meaning |
| --- | --- |
| `MaxConcurrentCalls` | Maximum number of messages processed simultaneously by this listener. |
| `PrefetchCount` | How many messages the pump pre-loads. Raising it trades latency and redelivery risk for throughput. |
| `MessageLockTimeout` | The maximum time processing may take. Also the deadline on the `CancellationToken` handed to your handler. |
| `DeadLetterDeliveryLimit` | Attempts before the message is dead-lettered. Must be lower than the queue's own dead-letter limit to have any effect. |

Exact behaviour is transport-dependent — the transport pages note the specifics — but the meaning of
each knob is the same everywhere.

!!! tip "Settings types are shared, not per-processor"
    Because settings are just a type, several processors can reference the same one. Define a small
    set (`HighThroughputSettings`, `SlowSettings`) and reuse them rather than writing one per
    processor.

### The cancellation token

The token passed to `ProcessAsync` is cancelled when `MessageLockTimeout` elapses (measured from
when the message was fetched), and also when the host shuts down. Honour it: a handler that ignores
it keeps running past the point where the transport considers the message lost, and the message will
be redelivered while your handler is still working on it.

### Long-running work: extending the lock

For work that legitimately runs longer than you want a single transport lock to last, have your
settings also implement `IExtendMessageLockTimeout`:

```csharp
public class LongRunningSettings : IProcessingSettings, IExtendMessageLockTimeout
{
    public int MaxConcurrentCalls => 1;
    public int PrefetchCount => 0;
    public TimeSpan MessageLockTimeout => TimeSpan.FromHours(2);   // total processing budget
    public int DeadLetterDeliveryLimit => 2;

    public TimeSpan ExtensionDuration => TimeSpan.FromMinutes(5);  // lock actually taken
    public TimeSpan ExtensionInterval => TimeSpan.FromMinutes(1);  // renewed this often
}
```

The two timeouts play different roles: `ExtensionDuration` is the short lock taken on the transport,
renewed every `ExtensionInterval` while your handler runs, and `MessageLockTimeout` remains the total
budget after which the handler's token is cancelled. The benefit is that a crashed host releases the
message after `ExtensionDuration` instead of after the full two hours.

!!! warning "Lock extension needs a middleware, and only works on Azure Storage Queues"
    Implementing the interface is not enough. You must register the middleware yourself:

    ```csharp
    services.AddMiddleware<ExtendMessageLockDurationMiddleware>();
    ```

    Renewal also requires the transport to support changing a lock mid-flight, which today only
    Azure Storage Queues does. See
    [errors and dead-lettering](../features/error-handling.md#message-locks).

!!! danger "Do not use `IExtendMessageLockTimeout` on the PostgreSQL transport"
    On PostgreSQL it is not merely inert — it is harmful. The message pump takes the *fetch* lock for
    `ExtensionDuration` whenever the settings implement this interface, but nothing on that transport
    can renew it. With the values above the row becomes visible again after 5 minutes while your
    handler runs on toward its 2-hour budget, so the message is picked up and processed concurrently,
    and then dead-lettered.

    Use plain `IProcessingSettings` with a `MessageLockTimeout` long enough for the work, or move the
    message to Azure Storage Queues. Service Bus renews locks itself and needs neither.

## See also

- [Errors and dead-lettering](../features/error-handling.md) — what happens when a handler throws.
- [Singleton processing](../features/singleton-processing.md) — restricting a processor to one
  concurrent execution across all instances.
- [Middleware pipeline](middleware.md) — what runs around your handler.
