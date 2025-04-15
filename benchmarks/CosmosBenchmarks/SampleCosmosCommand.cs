using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;

namespace Cosmos.Benchmarks


//Commands
class SampleCosmosCommand : ICosmosCommand
{
    public required string MessageBody { get; set; }
}

class SampleCosmosMessageMapping : IMessageMapping<SampleCosmosCommand>
{
    public string QueueName => "test-command";
}

class PostgresCommandProcessor :
    IProcessCommand<SampleCosmosCommand, CosmosProcessingSetting>
{
    public Task ProcessAsync(SampleCosmosCommand message, CancellationToken cancellationToken)
    {
        ProcessedMessages.Increment(message.MessageBody);

        return Task.CompletedTask;
    }
}

