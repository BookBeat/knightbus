Transports
==========

Transports determine how your message is transported from Client to Processor. All current transports are listed here, but it's also fairly easy to extend KnightBus with other transports.
A central concept is that each message is tagged with the transport it should use, for instance if you want to send a Command over Azure ServiceBus, implement the interface IServiceBusCommand.

Transport Configuration
-----------------------

TODO

Azure Service Bus
-----------------

https://azure.microsoft.com/en-us/services/service-bus/

Azure Storage Bus
-----------------

* commands
* attachments
* saga store
* singleton locks

https://azure.microsoft.com/en-us/services/storage/queues/

Redis
-----------------

* commands
* events
* attachments
* saga store

The Redis transport supports both commands and events and is a very high performance transport. 

KnightBus implements the Circular list pattern where all messages sent are stored on a list and when processed they are moved to another list making sure that no messages are lost in transit.

Pub/Sub is enabled by that the clients will find out what subscribers exists and publish to specific queue. An event that has three listeners will be published to three separed queues.

LavinMQ
-----------------

* commands
* events
* dead-lettering
* scheduling

https://lavinmq.com/

The LavinMQ transport speaks AMQP 0-9-1 (via ``RabbitMQ.Client``) and therefore also works against RabbitMQ and CloudAMQP; LavinMQ is the tested target. Tag a message with ``ILavinMQCommand`` or ``ILavinMQEvent`` and enable it with ``UseLavinMQ()`` + ``UseTransport<LavinMQTransport>()``.

Commands map to a durable queue (published via the default exchange). Events map to a fanout exchange with one bound queue per ``IEventSubscription``. Dead-lettering uses LavinMQ's native ``x-dead-letter-exchange`` and ``x-delivery-limit`` queue arguments, and scheduling uses the built-in ``x-delayed-message`` exchange.

