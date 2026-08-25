# NATS

The only KnightBus transport with request/response, and the only one that can stream many replies to a
single request.

```bash
dotnet add package KnightBus.Nats
dotnet add package KnightBus.Nats.Messages
```

## Registration

```csharp
services
    .UseNats(config => config.ConnectionString = "localhost")
    .RegisterProcessors()
    .UseTransport<NatsTransport>();
```

`ConnectionString` is a shortcut for the NATS client's `Options.Url`. For anything more advanced,
mutate `Options` directly:

```csharp
services.UseNats(config =>
{
    config.Options.Url = "nats://localhost:4222";
    config.Options.MaxReconnect = 10;
});
```

Start a broker locally with:

```bash
docker run -p 4222:4222 -ti nats:latest
```

## Messages

| Interface | Kind |
| --- | --- |
| `INatsCommand` | Command |
| `INatsEvent` | Event |
| `INatsRequest` | Request — the sender waits for a reply |

## Client

```csharp
var bus = scope.ServiceProvider.GetRequiredService<INatsBus>();

await bus.Send(new SampleCommand());
await bus.Publish(new SampleEvent());

var reply = await bus.RequestAsync<LookupRequest, LookupReply>(new LookupRequest());

foreach (var item in bus.RequestStream<LookupRequest, LookupReply>(new LookupRequest()))
{
    Console.WriteLine(item.Value);
}
```

!!! note "The method names differ here"
    NATS uses **`Send`** and **`Publish`** without the `Async` suffix that other transports use, and
    neither is generic — they take `INatsCommand`/`INatsEvent` and resolve the mapping from the
    runtime type. `RequestStream` returns a synchronous `IEnumerable<TResponse>` that blocks as it
    enumerates, even though the processor produces items asynchronously.

## Request/response

A request processor returns a single value:

```csharp
public class LookupProcessor : IProcessRequest<LookupRequest, LookupReply, DefaultSettings>
{
    public Task<LookupReply> ProcessAsync(LookupRequest message, CancellationToken cancellationToken) =>
        Task.FromResult(new LookupReply { Value = "42" });
}
```

## Streaming responses

A stream request processor yields many, each sent to the caller as it is produced. This is the reason
to pick NATS: a long query can start returning rows immediately rather than buffering the whole
result.

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
            yield return new LookupReply { Value = $"Reply {i}" };
        }
    }
}
```

The `[EnumeratorCancellation]` attribute matters — without it the cancellation token is not propagated
into the iterator.

Both request forms are synchronous in the sense that the caller is waiting, so keep handlers fast and
treat the caller's timeout as your real deadline.

## Limitations

NATS is the leanest transport in KnightBus:

| Feature | Status |
| --- | --- |
| Deferred delivery | Not supported |
| Dead letter queue | Not supported |
| Saga store | Not shipped — use another store |
| Management API | Not supported |
| Attachment provider | Not shipped — use another provider |

There is no dead letter queue, so `DeadLetterDeliveryLimit` has nowhere to move a failing message.
Plan for poison messages explicitly, for example by catching the failure in your handler and
forwarding the payload somewhere durable.

## Attachments and sagas

Both work on NATS as long as another package supplies the implementation. Mixing packages like this
is normal:

```csharp
services
    .UseBlobStorage(storageConnectionString)
    .UseBlobStorageAttachments()
    .UseBlobStorageSagas()
    .UseNats(config => config.ConnectionString = natsConnection)
    .RegisterProcessors()
    .UseTransport<NatsTransport>();
```

## Serialization

Defaults to `NewtonsoftSerializer`.

## Example

[`KnightBus.Samples.Nats`](https://github.com/BookBeat/knightbus/tree/master/samples/KnightBus.Samples.Nats)
demonstrates a streaming request, events with two subscriptions, and attachments stored in Blob
Storage.
