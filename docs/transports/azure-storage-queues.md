# Azure Storage Queues

Cheap, durable and simple. Commands only — no pub/sub — but it is the transport with the best support
for long-running work, and the package also supplies the attachment provider, saga store and
singleton lock manager that other transports borrow.

```bash
dotnet add package KnightBus.Azure.Storage
dotnet add package KnightBus.Azure.Storage.Messages
```

## Registration

=== "Connection string"

    ```csharp
    services
        .UseBlobStorage(connectionString)
        .RegisterProcessors()
        .UseTransport<StorageTransport>();
    ```

=== "Managed identity"

    ```csharp
    services
        .UseBlobStorage("mystorageaccount", new ManagedIdentityCredential())
        .RegisterProcessors()
        .UseTransport<StorageTransport>();
    ```

The package name says "blob storage" because the same configuration covers queues, the attachment
blobs and the saga blobs.

## Messages

Only one marker interface — this transport has no events:

```csharp
public class ResizeImage : IStorageQueueCommand
{
    public string ImageId { get; set; }
}

public class ResizeImageMapping : IMessageMapping<ResizeImage>
{
    public string QueueName => "resize-image";
}
```

## Client

```csharp
var bus = scope.ServiceProvider.GetRequiredService<IStorageBus>();

await bus.SendAsync(new ResizeImage { ImageId = "1" });
await bus.ScheduleAsync(new ResizeImage { ImageId = "2" }, TimeSpan.FromMinutes(30));
```

`ScheduleAsync` sets the message's visibility delay. There is no batch overload and no way to cancel
a deferred message — for that, use [Service Bus](azure-service-bus.md#deferred-messages).

## What gets created

One logical KnightBus queue is three storage objects:

| Object | Purpose |
| --- | --- |
| Queue `{name}` | The queue itself, holding message properties. |
| Queue `{name}-dl` | Dead letters. |
| Blob container `{name}` | Message payloads and attachments. |

The payload lives in a blob and the queue message only references it, which is how this transport
sidesteps the 64 KB queue message limit entirely. It also means a queue message whose payload blob is
missing is deleted on read as unrecoverable.

Dead letter queues are hidden from the [management API](../features/management.md) listing, so they do
not appear as queues in their own right.

## Long-running work

This is the only transport that can extend a message lock while your handler runs, which makes it the
right choice for work measured in minutes or hours. Implement `IExtendMessageLockTimeout` on your
settings and register the middleware:

```csharp
public class LongRunningSettings : IProcessingSettings, IExtendMessageLockTimeout
{
    public int MaxConcurrentCalls => 1;
    public int PrefetchCount => 0;
    public TimeSpan MessageLockTimeout => TimeSpan.FromHours(4);   // total budget
    public int DeadLetterDeliveryLimit => 2;

    public TimeSpan ExtensionDuration => TimeSpan.FromMinutes(5);  // lock actually held
    public TimeSpan ExtensionInterval => TimeSpan.FromMinutes(1);  // renewal cadence
}
```

```csharp
services.AddMiddleware<ExtendMessageLockDurationMiddleware>();
```

The benefit over one enormous `MessageLockTimeout` is recovery time: if the host crashes, the message
becomes visible again after `ExtensionDuration` rather than after the full four hours. See
[extending the lock](../concepts/processors.md#long-running-work-extending-the-lock).

## Attachments

The Blob Storage attachment provider is the one most applications use, whatever transport carries the
messages:

```csharp
services
    .UseBlobStorage(connectionString)
    .UseBlobStorageAttachments();
```

Optional Brotli compression:

```csharp
services.UseBlobStorageAttachments(options =>
{
    options.EnableCompression = true;
    options.CompressionLevel = CompressionLevel.Optimal;
});
```

Compression is off by default and safe to enable on an existing store — whether a blob is compressed
is recorded in its name, so old attachments keep working. See
[attachments](../features/attachments.md).

## Saga store

```csharp
services.UseBlobStorageSagas();
```

State is stored in the `knightbus-sagas` container with the blob ETag providing optimistic
concurrency. Expiry is evaluated on read, so expired blobs are reported as not found but are not
deleted for you. See [sagas](../features/sagas.md).

## Singleton lock manager

Blob leases are KnightBus' only shipped distributed lock implementation, so this package is what
enables [singleton processing](../features/singleton-processing.md) and
[cron scheduling](../features/scheduling.md) — even for applications whose messages travel over
another transport entirely.

```csharp
services
    .UseBlobStorage(connectionString)
    .UseBlobStorageLockManager();
```

Locks are blobs under `knight-data/locks`. To change that, implement `IBlobLockScheme`:

```csharp
public class MyLockScheme : IBlobLockScheme
{
    public string ContainerName => "my-container";
    public string Directory => "my-locks";
}

services.UseBlobStorageLockManager(new MyLockScheme());
```

## Management

```csharp
services.UseBlobStorageManagement(connectionString);
```

`PeekScheduled` is not supported. Note that `MoveDeadLetters` returns the number of messages you
*asked* it to move rather than the number it actually moved, and that it removes messages from the
dead letter queue even when a requeue predicate rejects them.

## Serialization

Defaults to `NewtonsoftSerializer`. Queue messages are Base64-encoded by default for compatibility
with older storage clients; changing `MessageEncoding` requires passing a constructed
`StorageBusConfiguration` rather than using the configuration callback, since the property is
read-only on the interface.

## Example

[`KnightBus.Examples.Azure.Storage`](https://github.com/BookBeat/knightbus/tree/master/knightbus/examples/KnightBus.Examples.Azure.Storage)
shows attachments, the blob lock manager, the blob saga store and distributed tracing together. Run
it against [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) with
`UseDevelopmentStorage=true`.
