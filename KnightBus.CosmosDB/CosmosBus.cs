using KnightBus.Cosmos.Messages;

public interface ICosmosBus
{
    Task SendAsync<T>(T message, CancellationToken ct) where T : ICosmosCommand;
    //More things should be added later
}
