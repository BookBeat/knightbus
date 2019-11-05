Sagas
========

Sagas add state to your messagaging and allows you to handle long running processes within the same code.
A Saga is a long lived transaction that is started by one or more specific messages and is finished by one or more messages. The messages can be either events or commands.

Setup
-----

Setup KnightBus for Sagas by registering it in the host:

.. code-block:: c#

    var knightBusHost = new KnightBusHost()
    //Enable the StorageBus Transport
    .UseTransport(new StorageTransport(storageConnection)
    .Configure(configuration => configuration
        //Register our message processors without IoC using the standard provider
        .UseMessageProcessorProvider(new StandardMessageProcessorProvider()
            .RegisterProcessor(new SampleSagaMessageProcessor(client))
        )
        //Enable Saga support using the table storage Saga store
        .EnableSagas(new StorageTableSagaStore(storageConnection))
    );

    await knightBusHost.StartAsync();

Sample Implementation
---------------------

.. code-block:: c#

    class SampleSagaMessageProcessor: Saga<MySagaData>,
            IProcessCommand<SampleSagaMessage, SomeProcessingSetting>,
            IProcessCommand<SampleSagaStartMessage, SomeProcessingSetting>
        {
            private readonly IStorageBus _storageBus;

            public SampleSagaMessageProcessor(IStorageBus storageBus)
            {
                _storageBus = storageBus;
                //Map the messages to the Saga
                MessageMapper.MapStartMessage<SampleSagaStartMessage>(m=> m.Id);
                MessageMapper.MapMessage<SampleSagaMessage>(m=> m.Id);
            }
            public override string PartitionKey => "sample-saga";
            public async Task ProcessAsync(SampleSagaStartMessage message, CancellationToken cancellationToken)
            {
                Console.WriteLine(message.Message);
                await _storageBus.SendAsync(new SampleSagaMessage());
            }

            public async Task ProcessAsync(SampleSagaMessage message, CancellationToken cancellationToken)
            {
                Console.WriteLine("Counter is {0}", Data.Counter);
                if (Data.Counter == 5)
                {
                    Console.WriteLine("Finishing Saga");
                    await CompleteAsync();
                    return;
                }

                Data.Counter++;
                await UpdateAsync();
                await _storageBus.SendAsync(new SampleSagaMessage());
            }
        }

        // This is exposed as `Data` property to classes that implements Saga<MySagaData>
        class MySagaData
        {
            public int Counter { get; set; }
        }

        class SomeProcessingSetting : IProcessingSettings
        {
            public int MaxConcurrentCalls => 1;
            public int PrefetchCount => 1;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
            public int DeadLetterDeliveryLimit => 2;
        }

