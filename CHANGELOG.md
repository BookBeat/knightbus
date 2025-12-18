# CHANGELOG

# 2025-12-17
Added better support for scheduled messages, including ability to peek scheduled messages and cancel them by sequence number.

**Note:** Only ServiceBus supports cancelling scheduled messages

### KnightBus.Core.Management 3.0.0
* Added PeekScheduled method to IQueueManager
* Added optional property SequenceNumber to QueueMessage
### KnightBus.Azure.ServiceBus 23.0.0
* ScheduleAsync methods now returns corresponding sequence number(s)
* Added CancelScheduledAsync method to cancel scheduled messages by sequence number(s)
### KnightBus.Azure.ServiceBus.Management 3.0.0
### KnightBus.Azure.Storage.Management 3.0.0
### KnightBus.PostgeSql.Management 3.0.0
### KnightBus.PostgeSql.Management.Extensions 2.0.0
### KnightBus.Redis.Management 2.0.0
* Bump packages and implement PeekScheduled where applicable

# 2025-04-08
Add support for metadata on message attachments

### KnightBus.Core 17.1.0
### KnightBus.Messages 7.1.0
### KnightBus.Redis 14.1.0
### KnightBus.Azure.Storage 17.1.0

## 2025-03-28
Upgraded to .net 9. All packages now target net9.0

### Deleted packages
The following packages was never used and has been removed:
* KnightBus.MessagePack
* KnightBus.ProtoBufNet

## 2024-01-03
* Added pre processors of messages before they are sent
* Converter Attachments to using a pre processor
* Added support for distributed tracing using a pre processor
* Updated external dependencies
### KnightBus.Azure.ServiceBus 20.0.0
### KnightBus.Azure.Storage 15.0.0
### KnightBus.Nats 4.0.0
### KnightBus.Redis 11.0.0
### KnightBus.Core 15.1.0
### KnightBus.NewRelic 10.0.0

## 2023-04-24

### KnightBus.Azure.ServiceBus 18.1.0
### KnightBus.Azure.Storage 13.1.0
### KnightBus.Azure.Redis 9.1.0
### KnightBus.MessagePack 3.1.0
### KnightBus.Newtonsoft 3.1.0
### KnightBus.ProtoBufNet 4.1.0
### KnightBus.Schedule 11.1.0
### KnightBus.SqlServer 13.1.0
* Bump packages

## 2022-12-05

### KnightBus.Core 14.0.0
* Removed ConsoleWriter

### KnightBus.Host 14.0.1
* Use ILogger instead of ConsoleWriter

### KnightBus.Schedule 11.0.0
* Use ILogger instead of ConsoleWriter
* Changed ctor for SchedulingPlugin

## 2022-11-08

### KnightBus.*

#### Multiple major breaking changes
* Replaced all DI with Microsoft.Abstractions
* Replaced all Logging with Microsoft.Abstractions
* Removed packages:
  * KnightBus.Serilog
  * KnightBus.SimpleInjector
  * KnightBus.Microsoft.DependencyInjection

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
