using KnightBus.Cosmos.Messages;

namespace KnightBus.Cosmos;
public interface ICosmosBus
{
    
    //Send single command
    Task SendAsync<T>(T message, CancellationToken ct) where T : ICosmosCommand;
    
    //Send Multiple commands
    Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosCommand;
    
    //Publish one event
    Task PublishAsync<T>(T message, CancellationToken ct) where T : ICosmosEvent;
    
    //Publish multiple events
    Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosEvent;
    
    //Schedule one command
    Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand;
    
    //Schedule multiple commands
    Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand;
}

public class CosmosBus : ICosmosBus
{
    //Send a single command
    public Task SendAsync<T>(T message, CancellationToken ct) where T : ICosmosCommand
    {
        //To be implemented
        return Task.CompletedTask;
    }

    //Send multiple commands
    public Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosCommand
    {
        //To be implemented
        return Task.CompletedTask;
    }

    //Publish a single event
    public Task PublishAsync<T>(T message, CancellationToken ct) where T : ICosmosEvent
    {
        return Task.CompletedTask;
    }
    
    //Publish multiple events
    public Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosEvent
    {
        //To be implemented
        return Task.CompletedTask;
    }

    //Schedule a single command
    public Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand
    {
        //To be implemented
        return Task.CompletedTask;
    }

    //Schedule multiple commands
    public Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand
    {
        //To be implemented
        return Task.CompletedTask;
    }
}


