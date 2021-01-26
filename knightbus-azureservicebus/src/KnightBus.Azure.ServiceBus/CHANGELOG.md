# KnightBus.Azure.ServiceBus Changelog

## 9.0.0

- Removed `EnablePartitioning` from `ServiceBusCreationOptions` since it applied to all ServiceBus queues/topics in the assembly.
- Added `KnightBus.Azure.ServiceBus.Messages.IPartitionedMessage` to replace partitioning functionality, but for control of each ServiceBus queue/topic.
- For indicating if it should use partitioning, add `IPartitionedMessage` to `IServiceBusCommand`/`IServiceBusEvent` implementation instead.

Example:

```
public class CalculateSomethingCommand : IServiceBusCommand, IPartitionedMessage
{
    // ...
}
```