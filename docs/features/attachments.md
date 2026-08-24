# Attachments

Attachments let a message carry a payload far larger than the transport allows. The file is stored
out of band and only its id travels in the message body, so a 100 MB file can ride on a transport
with a 256 KB message limit.

Attachments are transport independent: the same mechanism works on Service Bus, Storage Queues, Redis
and NATS.

## Sending an attachment

Implement `ICommandWithAttachment` on the command alongside its transport interface:

```csharp
public class ImportFile : IServiceBusCommand, ICommandWithAttachment
{
    public string Description { get; set; }
    public IMessageAttachment Attachment { get; set; }
}
```

Then attach a stream when sending:

```csharp
await bus.SendAsync(new ImportFile
{
    Description = "Customer import",
    Attachment = new MessageAttachment(
        "customers.csv",
        "text/csv",
        File.OpenRead("customers.csv")
    ),
});
```

`MessageAttachment` also takes an optional metadata dictionary:

```csharp
new MessageAttachment(
    "customers.csv",
    "text/csv",
    stream,
    new Dictionary<string, string> { ["tenant"] = "acme" }
)
```

## Receiving an attachment

The processor needs no special interface. By the time `ProcessAsync` runs, `Attachment` is populated
and its `Stream` is open:

```csharp
public class ImportFileProcessor : IProcessCommand<ImportFile, DefaultSettings>
{
    public async Task ProcessAsync(ImportFile message, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(message.Attachment.Stream);
        var contents = await reader.ReadToEndAsync();
    }
}
```

`IMessageAttachment` exposes `Filename`, `ContentType`, `Length`, `Stream` and `Metadata`. `Length`
is `0` for non-seekable streams.

The stream is disposed for you once the message finishes, so do not hold on to it past the end of
`ProcessAsync`.

## Registering a provider

Attachments need a store, and **both the sender and the receiver must register one** — the sender
uploads, the receiver downloads.

=== "Azure Blob Storage"

    ```csharp
    services
        .UseBlobStorage(storageConnectionString)
        .UseBlobStorageAttachments();
    ```

    With optional compression:

    ```csharp
    services.UseBlobStorageAttachments(options =>
    {
        options.EnableCompression = true;
        options.CompressionLevel = CompressionLevel.Optimal;
    });
    ```

    Compression uses **Brotli** and is off by default. Compressed blobs get a `.brotli` suffix and
    `ContentEncoding: br`, and decompression on read is decided per blob by that suffix — so turning
    compression on is backwards compatible with attachments already in the store.

=== "Redis"

    ```csharp
    services
        .UseRedis(config => config.ConnectionString = redisConnectionString)
        .UseRedisAttachments();
    ```

    There is no options overload; Redis attachments are stored uncompressed.

Either provider works with any transport. Blob Storage is the usual choice even when the messages
travel over another transport — the NATS example uses NATS for messages and Blob Storage for
attachments — and `UseRedisAttachments()` is equally available to an application with no Redis
transport. Both calls register the same core `AttachmentMiddleware`; what differs is only where the
bytes land. The provider is chosen per host, not per transport, and the middleware resolves a single
`IMessageAttachmentProvider` — so register one, since a second registration silently wins over the
first.

## Lifecycle and cleanup

Attachments follow the fate of the message:

| Outcome | What happens to the attachment |
| --- | --- |
| Processed successfully | Deleted. A failed delete is logged as a warning and does not fail the message. |
| Processing failed | **Kept** — the retry needs it. |
| Dead-lettered | **Kept**, so the message can be inspected or requeued intact. |

!!! warning "Dead-lettered attachments are never cleaned up"
    KnightBus deliberately leaves attachments of dead-lettered messages in place. Nothing will ever
    delete them, so set a lifecycle policy on the container or key space if they must expire.

## How it works

On send, an `IMessagePreProcessor` uploads the attachment and puts its id in the `_attachments`
message property. On receive, `AttachmentMiddleware` reads that property, downloads the file and
assigns `message.Attachment` before your processor runs.

Two consequences follow:

- **Serializers must skip the `Attachment` property.** Both bundled serializers do. If you write your
  own, mirror that — see [serialization](../concepts/serialization.md#writing-your-own).
- **Attachments do not work on the PostgreSQL transport.** `PostgresBus` does not run pre-processors,
  so nothing uploads the attachment. Use another transport for messages with attachments, or store
  the payload yourself and send a reference.

## Custom providers

Implement `IMessageAttachmentProvider` to store attachments anywhere:

```csharp
public interface IMessageAttachmentProvider
{
    Task<IMessageAttachment> GetAttachmentAsync(string queueName, string id, CancellationToken ct = default);
    Task<string> UploadAttachmentAsync(string queueName, IMessageAttachment attachment, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(string queueName, string id, CancellationToken ct = default);
}
```

`UploadAttachmentAsync` returns the id that travels with the message. Register the provider plus the
middleware and pre-processor the built-in extensions register for you:

```csharp
services.AddSingleton<IMessageAttachmentProvider, MyAttachmentProvider>();
services.AddMiddleware<AttachmentMiddleware>();
services.AddSingleton<IMessagePreProcessor, AttachmentPreProcessor>();
```

## See also

- [Messages and mappings](../concepts/messages.md) — pre-processors in general.
- [Management API](management.md) — reading attachments of dead-lettered messages.
