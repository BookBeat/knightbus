.. KnightBus documentation master file, created by
   sphinx-quickstart on Wed Oct 10 12:11:58 2018.
   You can adapt this file completely to your liking, but it should at least
   contain the root `toctree` directive.

Welcome to KnightBus's documentation!
=====================================

*KnightBus is a fast, lightweight and extensible messaging framework that supports multiple active messaging transports*

When building BookBeat we soon discovered that there was no silver bullet messaging technology, each one had its own pros and cons. Reliability, performance, latency, scalability, pricing and capabilites made us build KnightBus so that we could choose transport on a per message basis. 

Features:

* **Multiple Transports**, active simultaneously on per message basis
* **Middleware**, write your own middleware to implement custom features
* **Attachments**, attach large files to your messages, transport independent
* **Singleton Processing**, make sure only one message is processed at a time regardless of number of instances running
* **Throttling**, both global and per message
* **IoC**, bring you own or use the default SimpleInjector

.. toctree::
   :maxdepth: 2
   :caption: Contents:

   quickstart
   host
   messages
   transports
   sagas
   versioning
   license
   credits
