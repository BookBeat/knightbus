Monitoring
==========

Using the middleware pattern KnightBus can monitor message processing with any tool you'd like.

Already available monitoring middlewares
----------------------------------------

* New Relic
* Application Insights

New Relic
~~~~~~~~~

Install the package `KnightBus.NewRelic` and configure `KnightBusHost` to use New Relic.

.. code-block:: c#

    var knightBus = new KnightBusHost()
        .UseTransport(...)
        .Configure(configuration => configuration
            .UseNewRelic()
            ...
        );
