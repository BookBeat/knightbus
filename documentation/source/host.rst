KnightBus Host
==============

The KnightBus host is the part of KnightBus responsible for invoking your MessageProcessors with the message sent on the transport.
All configuration options for the host is exposed through the IHostConfiguration property from Configure()


Middlewares
-----------

Middlewares enable you to easily control what happens when processing messages. Much of KnightBus's internal mechanisms are implemented as Middlewares.
Middlewares can be attached at either host or transport level. A host attached Middleware will be invoked for all transports where as a transport attached middleware will be specific for the transport only.

Default Middlewares
~~~~~~~~~~~~~~~~~~~

* ErrorHandlingMiddleware - Makes sure all exceptions are caught, logged and that the message is marked as failed. This Middleware is always added as the first Middleware.
* DeadLetterMiddleware - Dead letters messages that have failed to many times. 

Optional Included Middlewares
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

* ThrottlingMiddleware - Allows throttling number of concurrent messages.
* AttachmentMiddleware - Enables the use of message attachments.
* ExtendMessageLockDurationMiddleware - Enables automatic message lock extension. Useful for very long running messages where you don't want to take an extremly long lock directly on the transport. Currently only works with Azure Storage Queues. Enable by having your ProcessingSetting implement `IExtendMessageLockTimeout`.


Pipeline
~~~~~~~~

All Middlewares are executed in a Pipeline where the first component always is the ErrorHandlingMiddleware and the last is your actual implementation of the MessageProcessor

Since the Middlewares are executed in order, it is important to supply them in the order you want to execute them.

Writing your own Middleware
~~~~~~~~~~~~~~~~~~~~~~~~~~~

It's really easy to write your own custom Middleware, just implement the IMessageProcessorMiddleware and add the Middleware to the Pipeline on either host or transport level. Custom logging and performance monitoring are obvious use-cases.

.. code-block:: c#

    public class MyCustomMiddleware : IMessageProcessorMiddleware
    {
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            Console.WriteLine("This is before the next step in the Pipeline");
            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false); //Call the next Middleware in the Pipeline
            Console.WriteLine("This is after all later Middlewares have finished");
        }
    }

Logging
-------

You can bring your own logging framwork or use the supplied Microsoft.Abstractions implementation.

Dependency Injection
--------------------

KnightBus supports using your own IoC container or you can use the supplied Microsoft.Abstractions implementation.

Messages are lifestyle scoped per-message.


Singleton Locks
---------------

MessageProcessors can be marked as Singleton. This is done using the marker interface ISingletonProcessor.

A MessageProcessor marked with ISingletonProcessor will only run on one instance regardless of how you scale and will only process messages one at a time.

.. code-block:: c#

    public class SingletonCommandProcessor : IProcessCommand<SingletonCommand, Settings>, ISingletonProcessor
    {
        public Task ProcessAsync(SingletonCommand message, CancellationToken cancellationToken)
        {
            //This code will never run concurrently
            return Task.CompletedTask;
        }
    }

Since the SingletonLock is held globally a distributed locking mechanism must be supplied. By default KnightBus comes with an Azure implementation using Blob Storage leases.

To enable Singleton MessageProcessors simple supply the host with an ISingletonLockManager.

Hosting
-------

The KnightBus Host can be hosted anywhere where you can run Console Applications. Currently all of our KnightBus Hosts are deployed using Kubernetes Pods, Azure WebJobs and TopShelf Windows Services.