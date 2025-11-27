using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Host.Tests.Unit.ExampleProcessors;

public class SecondEventProcessor : IProcessEvent<TestEvent, TestSubscriptionTwo, TestTopicSettings>
{
    public Task ProcessAsync(TestEvent message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
