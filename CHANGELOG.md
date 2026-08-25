# CHANGELOG

# 2026-08-25

### Repository restructure
No package changed for consumers. The published package ids and versions are identical to what
`master` produced before this change, and the packaged file lists are byte-for-byte the same; only
where the sources live has moved. Two assemblies differ in metadata only: `KnightBus.Core` and
`KnightBus.Schedule` each dropped an `InternalsVisibleTo` naming a test project that does not exist.
* Projects moved from the merged-multi-repo layout (`knightbus/` plus eleven `knightbus-*` folders)
  into `src/`, `tests/` and `samples/`. `KnightBus.slnx` keeps the grouping in solution folders
* The sample applications were renamed `KnightBus.Examples.*` to `KnightBus.Samples.*`. They are not
  published packages
* Package versions are now managed centrally in `Directory.Packages.props`. Two dependencies that
  had drifted were unified: `Azure.Identity` to 1.17.1 in `KnightBus.PostgreSql.Extensions.Azure`,
  and `Microsoft.Extensions.Hosting` to 10.0.0 in the ServiceBus producer sample. The
  `Azure.Identity` bump pulls newer transitive dependencies into the two Azure extension packages —
  `Microsoft.IdentityModel.Abstractions` 6.35.0 to 8.14.0, `Microsoft.Identity.Client` 4.76.0 to
  4.78.0, `Azure.Core` 1.49.0 to 1.50.0, `System.ClientModel` 1.7.0 to 1.8.0. None of these appear
  in a nuspec, so what consumers resolve is unchanged
* Settings shared by the test projects moved to `tests/Directory.Build.props` and
  `tests/Directory.Build.targets`
* Fixed: the pre-release workflow decided what to publish by diffing `<Version>` lines, which
  treats a moved project file as a version bump on every package. It now matches projects by file
  name and compares the declared versions against the branch point
* Added `CONTRIBUTING.md`, `SECURITY.md`, `CODE_OF_CONDUCT.md`, issue and pull request templates,
  and a dependabot configuration. The pre-commit hook moved to `.githooks/`, installed with
  `git config core.hooksPath .githooks`

### KnightBus.PostgreSql 4.0.0
* PostgresBus now runs message pre-processors on send, schedule and publish, storing the returned properties in the `properties` column. Attachments and outgoing distributed tracing now work on this transport
* `publish_events` gained a three-argument overload carrying properties; the two-argument overload is kept for older publishers. A publisher that finds the new overload missing creates it and retries, which requires DDL rights on the knightbus schema — roles without them can create it up front via `QueueInitializer.InitPublishFunction`
* Breaking: the PostgresBus constructor takes `IEnumerable<IMessagePreProcessor>`. No change needed when resolving `IPostgresBus` through DI
* Fixed: ScheduleAsync with a fractional-second delay no longer fails under comma-decimal cultures for batches under 50 messages; the delay is now a typed interval parameter

### KnightBus.Redis 16.0.0
* Breaking: sagas are stored as Redis hashes with `data` and `stamp` fields instead of strings, under the same `sagas:{partitionKey}:{id}` key. 16.x fails with `WRONGTYPE` on every 15.x saga key, so delete `sagas:*` (with `SCAN`, not `KEYS`) before upgrading; draining alone is not enough, because a 15.x saga that was updated and never completed has no expiry. Do not roll back to 15.x with 16.x sagas in place — 15.x silently drops start messages for them and overwrites them on update
* `RedisSagaStore` detects concurrent writes: `Create` and `GetSaga` return a `ConcurrencyStamp`, and `Update`/`Complete` throw `SagaDataConflictException` when the stamp no longer matches. A null or empty stamp still writes or deletes unconditionally
* Updating a saga no longer clears its TTL; the expiry set by `Create` is kept until the saga completes or expires
* `Create`, `Update` and `Complete` run as single atomic Lua scripts and `Delete` is a single `DEL`, removing the read-then-delete race. The server must allow `EVAL`, `EVALSHA` and `SCRIPT LOAD`
* Saga methods validate the partition key, id and TTL and honour an already-cancelled `CancellationToken` before talking to Redis
* Added `RedisQueueConventions.GetSagaKey`

# 2026-08-24

### KnightBus.Azure.Storage 18.1.0
* Attachment compression and decompression now stream instead of buffering the whole attachment in memory; compressed results up to 4 MB are still buffered and sent as a single request
* Compressed attachments keep their uncompressed `Length` via blob metadata; their `Stream` is now read-forward only (`CanSeek` is `false`). `Length` is `0` for attachments from non-seekable sources and for compressed attachments stored by older versions
* The attachment container is created up front on the first upload per container per provider, instead of on a failed upload; a container deleted afterwards is recreated and the upload retried once

# 2025-12-19

### KnightBus.Core.Management 18.1.0
* Added CancelScheduledMessage to IQueueMessageSender
### KnightBus.Azure.ServiceBus.Management 3.1.0
### KnightBus.Azure.Storage.Management 3.1.0
### KnightBus.PostgeSql.Management 3.1.0
### KnightBus.PostgeSql.Management.Extensions 2.1.0
### KnightBus.Redis.Management 2.1.0
* Bump version and implement CancelScheduledMessage where applicable

# 2025-12-19
Added better support for scheduled messages, including ability to peek scheduled messages and cancel them by sequence number.

**Note:** Only ServiceBus supports cancelling scheduled messages

### KnightBus.Core.Management 18.0.0
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
* Bump version and implement PeekScheduled where applicable

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
