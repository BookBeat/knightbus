# CHANGELOG

## 2020-11-09

### KnightBus.Core

* (bugfix) Register mappings from assembly before adding assembly to list of mapped assemblies. This solves a race condition where we would try to get a message mapping before it was mapped through the assembly.
* (bugfix) Remove loop of types that had duplicate entries of IMessageMapping. This was due to an old auto-refactoring by Resharper.

## 2020-11-06

### KnightBus.SqlServer 6.0.0

* (breaking) Dropped support for .NET461 as `<TargetFramework>`

### KnightBus.Azure.ServiceBus 7.0.0

* (breaking) Dropped support for .NET461 as `<TargetFramework>`

### KnightBus.Host 9.0.0

* (breaking) Dropped support for .NET461 as `<TargetFramework>`
