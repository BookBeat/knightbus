# KnightBus.Core Changelog

# 19.0.0
* **Breaking:** `IChannelReceiver.StartAsync(stopFetching, cancellationToken)` replaces
  `StartAsync(cancellationToken)`. Cancelling `stopFetching` stops taking new messages while the ones
  being handled carry on, cancelling `cancellationToken` stops the receiver and cancels them. A
  receiver that cannot stop fetching on its own treats both alike. A singleton lock hand-over stops
  fetching first and waits for the running message, for up to the processor's `MessageLockTimeout`,
  before cancelling it
* **Breaking:** `GenericMessagePump.StartAsync` and `PumpAsync` take the same two tokens. Once fetching stops the pump
  fetches nothing more, even when it was waiting for room, and `CleanupResources` runs
* A message the pump fetched while it was stopping is handed to the pipeline with a cancelled token,
  which gives it back to the queue, instead of staying invisible until its lock runs out. On
  shutdown a handler can therefore be called with a token that is already cancelled

# 18.5.0
* `SingletonOptions` and `IServiceCollection.ConfigureSingletons(...)` make the poll interval, lock
  duration and renewal interval of singleton processors configurable. The defaults are unchanged.
  Instances waiting for a lock now add up to 20% random extra wait (`PollJitter`) so they do not poll
  in lock-step. Schedule locks keep their fixed timing
* Singleton locks can be spread over the hosts instead of piling up on the first one to start, see
  `docs/features/singleton-placement.md`. Nothing changes unless an `ISingletonMembership` is
  registered, which `UseBlobStorageLockManager` in `KnightBus.Azure.Storage` now does. A processor
  declares how heavy it is by implementing `ISingletonWeight` on its processing settings. New public
  types: `ISingletonWeight`, and for stores that keep track of the hosts `ISingletonMembership`,
  `SingletonMember` and `ISingletonLockInspector`

# 18.4.0
* `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`
  and `System.Text.Json` move to 9.0.19 on `net9.0` and 10.0.11 on `net10.0`, raising the floor
  consumers resolve against. No API change

# 18.3.0
* Nullable reference types are enabled. Public APIs carry nullability annotations; no signature changed.
  Implementations of the annotated extension points get new warnings until they match:
  `ITransportChannelFactory.Create` takes an `IEventSubscription?` (command processors have no
  subscription), `ITransportConfiguration.ConnectionString` is `string?` (null when using managed
  identity), `ISingletonLockManager.TryLockAsync` returns `Task<ISingletonLockHandle?>` (null already
  meant "lock held elsewhere"), `IPipelineInformation.Subscription` is nullable, and
  `SagaData.ConcurrencyStamp` is `string?` (stores that do not use stamps leave it unset)

# 7.1.0
* Add support for metadata in `IMessageAttachement`s

# 16.1.4
* (patch) Updated System.Text.Json version

# 16.1.3
* (patch) Updated System.Text.Json version

# 15.0.0
* Throw if etag differs when updating blob saga data

# 14.0.0
* Removed ConsoleWriter

## 8.4.0
* Added GetMapping for IMessage, to get IMessageMapper instance

## 8.3.0
* Added ISagaDuplicateDetected<> that can be used to handle the duplicated message before it is completed.  
    It can e.g. be used to re-schedule the message later on before it is deleted
