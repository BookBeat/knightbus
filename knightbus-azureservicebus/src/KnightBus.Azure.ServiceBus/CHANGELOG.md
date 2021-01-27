# KnightBus.Azure.ServiceBus Changelog

## 9.0.0

- Changed name from `ServiceBusCreationOptions` to `DefaultServiceBusCreationOptions` and implement `IServiceBusCreationOptions`.
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