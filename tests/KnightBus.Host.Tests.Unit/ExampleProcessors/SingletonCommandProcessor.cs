using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;

namespace KnightBus.Host.Tests.Unit.ExampleProcessors;

public class SingletonCommandProcessor
    : IProcessCommand<SingletonCommand, TestTopicSettings>,
        ISingletonProcessor
{
    public Task ProcessAsync(SingletonCommand message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
