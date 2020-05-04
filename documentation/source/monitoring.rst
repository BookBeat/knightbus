Monitoring
========

Using the middleware pattern KnightBus can monitor message processing with any tool you'd like. 

Already available monitoring middlewares
--------

New Relic
Application Insights

Liveness
--------
TcpAliveListenerPlugin offers monitoring for liveness using TCP that can be used for services that don't serve http. 

.. code-block:: c#

    var knightBus = new KnightBusHost()
        .Configure(configuration => configuration
            .UseTcpAliveListener(13000));

Example using Kubernetes:

.. code-block:: yaml

    livenessProbe:
     tcpSocket:
      port: {{  .Values.ports.liveness  }}
     initialDelaySeconds: 10
     periodSeconds: 10
     timeoutSeconds: 3
     successThreshold: 1
     failureThreshold: 5

See https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/#define-a-tcp-liveness-probe for more information on how to configure liveness with tcp.