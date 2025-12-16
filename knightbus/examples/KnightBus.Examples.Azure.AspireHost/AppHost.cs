var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Service Bus (will use emulator for local development)
var serviceBus = builder.AddAzureServiceBus("servicebus").RunAsEmulator();

// Add queues
serviceBus.AddServiceBusQueue("your-queue");
serviceBus.AddServiceBusQueue("other-queue");

// Add topic with subscriptions
var topic = serviceBus.AddServiceBusTopic("your-topic");
topic.AddServiceBusSubscription("subscription-1");
topic.AddServiceBusSubscription("subscription-2");

// Add the consumer (KnightBus host) - waits for Service Bus to be ready
builder
    .AddProject<Projects.KnightBus_Examples_Azure_ServiceBus>("servicebushost")
    .WithReference(serviceBus)
    .WaitFor(serviceBus);

// Add the producer - waits for Service Bus to be ready
builder
    .AddProject<Projects.KnightBus_Examples_Azure_ServiceBus_Producer>("producer")
    .WithReference(serviceBus)
    .WaitFor(serviceBus);

builder.Build().Run();
