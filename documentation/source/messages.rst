Messages
========

Everything processed using KnightBus is a considered a message and implements the IMessage interface.
Messages are transport dependent, and need to implement the proper interface for the transport.
All message haves a 1:1 relationship with a specific queue or topic. 

Commands
--------

Commands are messages that have a single recipient and is commanding the recipient to perform a task. 

All commands implement ICommand through the specific transport implementation, for instance to send a command using the Azure Service Bus, you must implement IServiceBusCommand

Events
------

Events are messages that have a single dispatcher and multiple recievers and is telling the recievers that something has happened.

All events implement IEvent through the specific transport implementation, for instance to publish an event using the Azure Service Bus, you must implement IServiceBusEvent

Message Mappings
----------------
All Messages must also have a corresponding MessageMapping. KnightBus uses this to determine where to send and recieve the message. You implement this simply by adding your own implementation of IMessageMapping<MessageToMap>. When sending receiving messages KnightBus automatically finds these mappings.

.. code-block:: c#

    public class MyMessage : IServiceBusCommand
    {
        public string Message { get; set; }
    }

    public class MyMessageMapping : IMessageMapping<MyMessage>
    {
        public string QueueName => "some-queue-name";
    }

The mapping class must reside in the same assembly as the message that is's mapping.

Sending Messages
----------------

To send a message you need to use the client for the specific message transport.

.. code-block:: c#

    var serviceBusClient = new ServiceBus(new ServiceBusConfiguration("connectionString"));
    var storageBusClient = new StorageBus(new StorageBusConfiguration("connectionString"));

    await serviceBusClient.SendAsync(new MyServiceBusMessage());
    await storageBusClient.SendAsync(new MyStorageBusMessage());

Message Attachments
-------------------

Regardless of the transport you can attach large files as attachments. Simply implement the ICommandWithAttachment interface on your command.
The default implementation is for Azure Blob Storage using the BlobStorageMessageAttachmentProvider class which needs to be configured on the ITransportConfiguration supplied to the client and the host. 

You can write your own implementation by implementing the IMessageAttachmentProvider interface. IMessageAttachmentProvider is separated by transport so you can use different attachment providers at the same time.

.. code-block:: c#

    public class MyMessage : IServiceBusCommand, ICommandWithAttachment
    {
        public string Message { get; set; }
        public IMessageAttachment Attachment { get; set; } //Here you can access the attached file
    }

Using Azure ServiceBus Creation Options Overrides For Queue/Topic
--------------------------------------------------------------

To tell the Azure ServiceBus queue/topic to override default creation options, add IServiceBusCreationOptions to IMessageMapping implementation.

.. code-block:: c#

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

