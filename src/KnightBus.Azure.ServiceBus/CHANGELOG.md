# KnightBus.Azure.ServiceBus Changelog

## 25.0.0
### Changed
- Requires `KnightBus.Core` 19.0.0: the queue and topic receivers implement
  `IChannelReceiver.StartAsync(stopFetching, cancellationToken)`. Cancelling `stopFetching` stops the
  processor from taking messages while the handlers carry on, so a singleton lock can be handed over
  without cancelling the message being processed
- A handler's token is cancelled when the receiver stops, when the message lock is lost, also while
  the receiver drains or before the message reached the handler, and once `MessageLockTimeout` has
  passed and the lock is no longer renewed, as on the other transports. It is no longer cancelled
  when the Service Bus processor stops on its own, which is what a drain does
- A receiver that is started again, as a singleton receiver is when it gets its lock back, keeps
  the client of each start apart: closing an earlier start no longer closes the new client
- Disposing the `ClientFactory` also closes its Service Bus connection, which stayed open before

## 24.2.0
### Changed
- `Azure.Identity` moves to 1.21.0 and `Azure.Messaging.ServiceBus` to 7.20.2, raising the floor
  consumers resolve against. No API change


## 24.1.0
### Changed
- Nullable reference types are enabled. `IServiceBusConfiguration.FullyQualifiedNamespace` and
  `Credential` are now `string?`/`TokenCredential?`, matching what `ServiceBusConfiguration` already
  declared: they are null in connection-string mode, as `ConnectionString` is under managed identity
- The queue and topic channel factories throw `InvalidOperationException` when a channel receiver
  cannot be constructed, instead of surfacing as a `NullReferenceException` at an unrelated call site


## 17.0.0
### Changed 
- Default is now shared connection of service bus client

## 16.0.0
### Added/Changed 
- Shared connection support of service bus client

## 15.1.2
### Changed 
- Simplify restart handling when using `IRestartTransportOnIdle`.

## 15.1.1
### Fixed
- Prevent potential memory leak when using `IRestartTransportOnIdle`.

## 15.1.0
### Added
- Possibility to automatically force restart of Azure ServiceBus receivers after a fixed idle period (i.e. no messages processed). Specified through `IRestartTransportOnIdle`.

## 9.0.0

- `ServiceBusCreationOptions` implements `IServiceBusCreationOptions`.
- To tell the Azure ServiceBus queue/topic to override default creation options, add IServiceBusCreationOptions to IMessageMapping implementation.

Example:

```
    public class MyMessage : IServiceBusCommand
    {
        public string Message { get; set; }
    }

    public class MyMessageMapping : IMessageMapping<MyMessage>, IServiceBusCreationOptions
    {
        public string QueueName => "your-queue";
		
        public bool EnablePartitioning => true;
        public bool SupportOrdering => false;
        public bool EnableBatchedOperations => true;
    }
```