# KnightBus.Core Changelog

# 18.5.0
* `SingletonOptions` and `IServiceCollection.ConfigureSingletons(...)` make the poll interval, lock
  duration and renewal interval of singleton processors configurable. The defaults are unchanged.
  Instances waiting for a lock now add up to 20% random extra wait (`PollJitter`) so they do not poll
  in lock-step. Schedule locks keep their fixed timing
* Singleton locks can be spread over the hosts instead of piling up on the first one to start, see
  `docs/features/singleton-placement.md`. It is opt in: nothing changes unless an
  `ISingletonPlacement` is registered (`UseBlobStorageSingletonPlacement` in
  `KnightBus.Azure.Storage`). New public types: `ISingletonPlacement`, `SingletonPlacementOptions`,
  `SingletonAssignment`, `ISingletonLockInspector` and `IDrainableChannelReceiver`
* `ErrorHandlingMiddleware` logs a handler that was stopped by its cancellation token (a shutdown, a
  lost lock or a lock hand-over) at Information instead of Error. The message is still abandoned. A
  cancellation that did not come from the token, such as a timeout, is still an error

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
