using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

// Queue and topic names must match the entities declared in Config.json

public class StateHandlerCommand : IServiceBusCommand
{
    public string? Message { get; set; }
}

public class StateHandlerCommandMapping : IMessageMapping<StateHandlerCommand>
{
    public string QueueName => "state-handler";
}

public class BusCommand : IServiceBusCommand
{
    public string? Message { get; set; }
}

public class BusCommandMapping : IMessageMapping<BusCommand>
{
    public string QueueName => "bus-commands";
}

public class BusEvent : IServiceBusEvent
{
    public string? Message { get; set; }
}

public class BusEventMapping : IMessageMapping<BusEvent>
{
    public string QueueName => "bus-events";
}

public class HostCommand : IServiceBusCommand
{
    public string? Message { get; set; }
}

public class HostCommandMapping : IMessageMapping<HostCommand>
{
    public string QueueName => "host-commands";
}

public class FailingHostCommand : IServiceBusCommand { }

public class FailingHostCommandMapping : IMessageMapping<FailingHostCommand>
{
    public string QueueName => "host-failing";
}

public class HostEvent : IServiceBusEvent
{
    public string? Message { get; set; }
}

public class HostEventMapping : IMessageMapping<HostEvent>
{
    public string QueueName => "host-events";
}

public class HostEventSubscription : IEventSubscription<HostEvent>
{
    public string Name => "host-sub";
}
