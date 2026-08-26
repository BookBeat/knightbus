# KnightBus.Core Changelog

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
