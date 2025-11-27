using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;

namespace KnightBus.Host.Tests.Unit.ExampleProcessors;

public class SingletonEventProcessor
    : IProcessEvent<TestEvent, TestSubscription, TestTopicSettings>,
        ISingletonProcessor
{
    public Task ProcessAsync(TestEvent message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
