# Sagas

A saga is a long-running workflow with state that survives between messages. Several messages, each
processed independently and possibly on different instances, cooperate on one piece of persisted
state until the workflow completes.

Use a saga when you need to coordinate a sequence — wait for three confirmations before shipping,
retry a multi-step provisioning process, count progress across a batch — without inventing your own
state table and concurrency control.

## Enabling sagas

Register a saga store. Four packages ship one, each with its own registration extension. The package
a store ships in does not tie it to that transport — pick whichever store suits the workload:

=== "Azure Blob Storage"

    ```csharp
    services
        .UseBlobStorage(storageConnectionString)
        .UseBlobStorageSagas();
    ```

=== "PostgreSQL"

    ```csharp
    services
        .UsePostgres(config => config.ConnectionString = connectionString)
        .UsePostgresSagaStore();
    ```

=== "Redis"

    ```csharp
    services
        .UseRedis(config => config.ConnectionString = connectionString)
        .UseRedisSagaStore();
    ```

=== "SQL Server"

    ```csharp
    services.UseSqlServerSagaStore(connectionString);
    ```

The store is independent of the transport carrying the messages — SQL Server has a saga store and no
transport at all, and it is perfectly normal to run messages over Service Bus with state in
PostgreSQL.

!!! warning "Exactly one saga store per host"
    Registering a second store throws `An instance of ISagaStore is already registered` at startup.

## Writing a saga

A saga processor derives from `Saga<TData>` **and** implements a processor interface per message it
handles:

```csharp
public class OrderSagaData   // must have a parameterless constructor
{
    public int ConfirmationsReceived { get; set; }
}

public class OrderSaga
    : Saga<OrderSagaData>,
        IProcessCommand<StartOrder, DefaultSettings>,
        IProcessCommand<OrderConfirmed, DefaultSettings>
{
    private readonly IServiceBus _bus;

    public override string PartitionKey => "order-saga";
    public override TimeSpan TimeToLive => TimeSpan.FromDays(1);

    public OrderSaga(IServiceBus bus)
    {
        _bus = bus;

        MessageMapper.MapStartMessage<StartOrder>(m => m.OrderId);
        MessageMapper.MapMessage<OrderConfirmed>(m => m.OrderId);
    }

    public async Task ProcessAsync(StartOrder message, CancellationToken cancellationToken)
    {
        // Data is already initialised here — this message started the saga
        await _bus.SendAsync(new RequestConfirmation { OrderId = message.OrderId });
    }

    public async Task ProcessAsync(OrderConfirmed message, CancellationToken cancellationToken)
    {
        Data.ConfirmationsReceived++;

        if (Data.ConfirmationsReceived < 3)
        {
            await UpdateAsync(cancellationToken);
            return;
        }

        await CompleteAsync(cancellationToken);
    }
}
```

Four things are required.

### 1. `PartitionKey`

A constant identifying the saga *type*. It must be the same for every instance of this saga, and is
used to partition storage. Give each saga class its own value.

### 2. `TimeToLive`

How long an incomplete saga instance is kept before the store expires it. This is your safety net
against workflows that never finish — pick a value comfortably longer than the slowest legitimate run.

### 3. Message mappings

In the constructor, tell the saga how to extract the saga id from each message:

```csharp
MessageMapper.MapStartMessage<StartOrder>(m => m.OrderId);
MessageMapper.MapMessage<OrderConfirmed>(m => m.OrderId);
```

Every message the saga handles needs a mapping, or you get a `SagaMessageMappingNotFoundException`.
The value returned is the saga instance id, so all messages belonging to one workflow must return the
same string.

**`MapStartMessage` creates the saga; `MapMessage` requires it to exist already.** A non-start message
arriving before the saga exists fails with `SagaNotFoundException`.

### 4. Persisting state

`Data` is your state object. Mutating it is not enough — call `UpdateAsync` to write it, and
`CompleteAsync` when the workflow is done:

```csharp
Data.ConfirmationsReceived++;
await UpdateAsync(cancellationToken);   // persist
...
await CompleteAsync(cancellationToken); // finish; state is removed
```

A saga that never completes lingers until its `TimeToLive` expires.

## Concurrency

!!! danger "Only the Blob and Redis stores detect concurrent writes"
    Saga state carries a `ConcurrencyStamp`, but of the four shipped stores **only `BlobSagaStore`
    and `RedisSagaStore` populate and check it**. On the PostgreSQL and SQL Server stores,
    `UpdateAsync` is an unconditional overwrite: two messages for the same saga processed
    simultaneously will both succeed, and the second silently discards the first one's changes.
    `SagaDataConflictException` is never raised by those two.

    If a saga can receive concurrent messages and correctness depends on not losing an update, you
    must either use the Blob or Redis store or serialize processing with `MaxConcurrentCalls => 1`
    on the saga's settings. Making the update idempotent is not sufficient on its own — a lost update
    is lost regardless of how the write is shaped. With Redis the check is only as durable as the key,
    so read the [eviction and replication caveat](../transports/redis.md#saga-store) first.

Where concurrency *is* detected — the Blob and Redis stores — the losing write throws
`SagaDataConflictException`. That propagates like any other handler failure, so the message is retried
and picks up the newer state. The work already done in the losing attempt runs twice, so keep saga
handlers cheap or idempotent. If the loser was the saga's *start* message, the middleware also deletes
the saga before the exception propagates (see
[failure during the start message](#failure-during-the-start-message)), which discards the winning
write as well; the retry then starts the saga afresh.

## Choosing a store

| Store | Concurrent-write detection | Expiry | Notes |
| --- | --- | --- | --- |
| `BlobSagaStore` | **Yes** — blob ETag | Checked on read; blobs are not deleted | Container `knightbus-sagas`. |
| `PostgresSagaStore` | **No** — last write wins | Checked on read; rows are not deleted | Table `knightbus.sagas`, created on demand. |
| `SqlServerSagaStore` | **No** — last write wins | Checked on read; rows are not deleted | Table `dbo.Sagastore`, created on demand. See size limits below. |
| `RedisSagaStore` | **Yes** — concurrency stamp | Native Redis TTL, preserved across updates; keys are removed on complete or expiry | Hash `sagas:{partitionKey}:{id}` with `data` and `stamp` fields. |

!!! warning "SQL Server store size limits"
    `dbo.Sagastore` stores the partition key and id as `NVARCHAR(50)` and the serialized state as
    `NVARCHAR(4000)`. Anything longer fails to persist, so keep SQL Server saga state small.

Expiry in the Blob, PostgreSQL and SQL Server stores is evaluated when the saga is *read*: an expired
saga reports as not found, but its row or blob stays. Clean up periodically if that matters. Redis
uses native key expiry, so expired sagas are removed by Redis itself.

## Duplicate starts

If a start message arrives for a saga that already exists, the saga is not restarted. By default this
is logged and the message is completed.

To take control, implement `ISagaDuplicateDetected<T>` for the start message:

```csharp
public class OrderSaga
    : Saga<OrderSagaData>,
        IProcessCommand<StartOrder, DefaultSettings>,
        ISagaDuplicateDetected<StartOrder>
{
    public Task ProcessDuplicateAsync(StartOrder message, CancellationToken cancellationToken)
    {
        // Runs instead of ProcessAsync when the saga already exists
        return Task.CompletedTask;
    }
}
```

The message is completed after your hook runs either way — the hook is for reacting to the duplicate
(logging, notifying, compensating), not for rejecting it.

## Failure during the start message

If the handler for a **start** message throws, KnightBus deletes the newly created saga before the
exception propagates. Without that, the retry would see the saga as already started and take the
duplicate path, leaving the workflow permanently stuck at step zero.

If the delete itself fails it is logged as a warning and the handler's original exception still
surfaces — retries then see a duplicate until the saga's `TimeToLive` expires.

## Exceptions

| Exception | Meaning |
| --- | --- |
| `SagaNotFoundException` | A non-start message arrived for a saga that does not exist (or has expired). |
| `SagaAlreadyStartedException` | A start message arrived for a saga that already exists. Handled internally — see [duplicate starts](#duplicate-starts). |
| `SagaDataConflictException` | Optimistic concurrency conflict on write. Raised by the Blob and Redis stores. |
| `SagaStorageFailedException` | The underlying store failed. |
| `SagaMessageMappingNotFoundException` | A message reached the saga with no `MapMessage`/`MapStartMessage` registered. |

The first four derive from `SagaException`. `SagaMessageMappingNotFoundException` does **not** — it
derives directly from `Exception` and lives in `KnightBus.Core.Sagas` rather than
`KnightBus.Core.Sagas.Exceptions`, so `catch (SagaException)` will not catch a missing message
mapping.

## Custom stores

Implement `ISagaStore` and register it with `EnableSagas<MyStore>()`:

```csharp
public interface ISagaStore
{
    Task<SagaData<T>> GetSaga<T>(string partitionKey, string id, CancellationToken ct);
    Task<SagaData<T>> Create<T>(string partitionKey, string id, T data, TimeSpan ttl, CancellationToken ct);
    Task Update<T>(string partitionKey, string id, SagaData<T> sagaData, CancellationToken ct);
    Task Complete<T>(string partitionKey, string id, SagaData<T> sagaData, CancellationToken ct);
    Task Delete(string partitionKey, string id, CancellationToken ct);
}
```

The contract expectations are: `Create` throws `SagaAlreadyStartedException` if the id exists,
`GetSaga` throws `SagaNotFoundException` if it does not, and `Update`/`Complete` throw
`SagaDataConflictException` when the `ConcurrencyStamp` no longer matches.

Of the shipped stores, `BlobSagaStore` and `RedisSagaStore` honour that last expectation; PostgreSQL
and SQL Server do not. A custom store should treat a null or empty `ConcurrencyStamp` as an
unconditional write and report a missing saga as `SagaNotFoundException` before checking the stamp —
the shared conformance tests expect both.

## See also

- [Message processors](../concepts/processors.md) — the interfaces a saga combines with.
- [Middleware pipeline](../concepts/middleware.md) — `SagaMiddleware` loads state before your handler.
