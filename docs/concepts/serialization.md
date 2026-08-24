# Serialization

Serialization is a property of the transport configuration, with a per-message override. The contract
is `IMessageSerializer`:

```csharp
public interface IMessageSerializer
{
    byte[] Serialize<T>(T message);
    T Deserialize<T>(ReadOnlySpan<byte> serialized);
    T Deserialize<T>(ReadOnlyMemory<byte> serialized);
    Task<T> Deserialize<T>(Stream serialized);
    string ContentType { get; }
}
```

## Bundled serializers

| Serializer | Package | Notes |
| --- | --- | --- |
| `MicrosoftJsonSerializer` | `KnightBus.Core` | `System.Text.Json`. Accepts a `JsonSerializerOptions` in its constructor. |
| `NewtonsoftSerializer` | `KnightBus.Newtonsoft` | `Newtonsoft.Json`. More forgiving of loosely-typed payloads. |

Both report `application/json` as their content type.

!!! info "Protobuf and MessagePack were removed"
    The `KnightBus.ProtoBufNet` and `KnightBus.MessagePack` packages no longer ship. Implement
    `IMessageSerializer` yourself if you need a binary format.

## Defaults differ per transport

This catches people out, so it is worth stating plainly: **PostgreSQL defaults to
`System.Text.Json`; every other transport defaults to Newtonsoft.**

| Transport | Default serializer |
| --- | --- |
| Azure Service Bus | `NewtonsoftSerializer` |
| Azure Storage Queues | `NewtonsoftSerializer` |
| Redis | `NewtonsoftSerializer` |
| NATS | `NewtonsoftSerializer` |
| PostgreSQL | `MicrosoftJsonSerializer` |

The practical consequence is that the four Newtonsoft-defaulting transports pull in the
`KnightBus.Newtonsoft` package, and that a message contract moved between transports can change
serialization behaviour — most visibly around casing, nulls and enum handling.

## Changing the serializer for a transport

Set `MessageSerializer` on the transport's configuration:

```csharp
services.UseServiceBus(config =>
{
    config.ConnectionString = connectionString;
    config.MessageSerializer = new MicrosoftJsonSerializer();
});
```

To keep `System.Text.Json` but change its behaviour, pass options:

```csharp
config.MessageSerializer = new MicrosoftJsonSerializer(
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
);
```

## Changing the serializer for one message

Put `ICustomMessageSerializer` on the message's **mapping**, not on the message and not on the
processor:

```csharp
public class LegacyCommandMapping : IMessageMapping<LegacyCommand>, ICustomMessageSerializer
{
    public string QueueName => "legacy-command";
    public IMessageSerializer MessageSerializer => new NewtonsoftSerializer();
}
```

This is the migration tool of choice: it lets one queue keep its historical format while everything
else moves to a new default.

!!! warning "Not honoured on the PostgreSQL send path"
    `PostgresBus` captures the configured serializer when it is constructed, so
    `ICustomMessageSerializer` is ignored when *sending* over PostgreSQL. The receiving side does
    honour it, which makes the override asymmetric — a message written with the transport default
    would be read back with the custom serializer. Avoid per-message serializers on PostgreSQL.

## Writing your own

Implement `IMessageSerializer` and hand it to a transport configuration or a mapping. One rule
matters: **a serializer must not serialize the `Attachment` property** of
`ICommandWithAttachment` messages. The attachment travels out of band and the message body only
carries its id.

Both bundled serializers do this already — `MicrosoftJsonSerializer` registers a converter that
always writes null for `IMessageAttachment`, and `NewtonsoftSerializer` uses an
`IgnoreAttachmentsResolver` contract resolver. Mirror that behaviour if you write your own and intend
to use [attachments](../features/attachments.md).

## See also

- [Messages and mappings](messages.md) — where mappings live and why.
- [Attachments](../features/attachments.md) — the out-of-band payload mechanism.
