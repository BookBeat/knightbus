# CHANGELOG

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
