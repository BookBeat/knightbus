# CHANGELOG

## 2021-12-21

### KnightBus.SqlServer 9.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.SimpleInjector 11.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Serilog 9.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Schedule 8.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.ProtobufNet 3.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Netwonsoft 2.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.NewRelic 5.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Microsoft.DependencyInjection 11.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Messages 5.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.MessagePack 2.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Host 12.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Core 11.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.ApplicationInsights 8.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Redis.Messages 4.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Redis 7.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Azure.Storage.Messages 4.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Azure.Storage 11.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Azure.ServiceBus.Messages 4.0.0

* Change target framework from net5.0 -> net6.0

### KnightBus.Azure.ServiceBus 14.0.0

* Change target framework from net5.0 -> net6.0

### 

## 2021-05-05

### KnightBus.Azure.ServiceBus 13.3.0

* Add batch `ScheduleAsync<T>(IEnumerable<T>, TimeSpan, CancellationToken)`

## 2021-05-04

### KnightBus.Azure.ServiceBus 13.2.0

* Add batch `PublishEventsAsync<T>(IEnumerable<T>, CancellationToken)`

## 2021-05-03

### KnightBus.Azure.ServiceBus 13.1.0

* Use Service Bus internal framework for making sure batches of messages do not exceed maximum batch size

### KnightBus.Azure.ServiceBus 13.0.0

* Change IList<T> -> IEnumerable<T> for SendAsync

## 2021-04-06

### KnightBus.Core 9.0.0

 * Switch to Microsoft json serialization and remove Newtonsoft
 * Change interface for serialization to support binary
 * Add support for protobuf-net
 * Add support for marking messages with serialization format.

### KnightBus.ProtobufNet 1.0.0

 * Initial release

## 2021-03-25

### KnightBus.Azure.ServiceBus 10.0.0

 * Updated to the new Azure.Messaging.ServiceBus package
 * Removed internal retry mechanism for ServiceBus
 * Removed custom code for handling graceful shutdown since it's now build into the SDK

### KnightBus.Azure.Storage 8.0.0

 * Removed support for TableStorage
 * Updated to the new Azure.Storage.Blobs and Azure.Storage.Queues packages

## 2020-11-09

### KnightBus.Core 8.3.2

* (bugfix) Register mappings from assembly before adding assembly to list of mapped assemblies. This solves a race condition where we would try to get a message mapping before it was mapped through the assembly.
* (bugfix) Remove loop of types that had duplicate entries of IMessageMapping. This was due to an old auto-refactoring by Resharper.

## 2020-11-06

### KnightBus.SqlServer 6.0.0

* (breaking) Dropped support for .NET461 as `<TargetFramework>`

### KnightBus.Azure.ServiceBus 7.0.0

* (breaking) Dropped support for .NET461 as `<TargetFramework>`

### KnightBus.Host 9.0.0

* (breaking) Dropped support for .NET461 as `<TargetFramework>`
