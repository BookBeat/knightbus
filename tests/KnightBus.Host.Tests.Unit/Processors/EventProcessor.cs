using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Host.Tests.Unit.Processors
{
    public class EventProcessor :
        IProcessEvent<TestEvent, TestSubscription, TestTopicSettings>
    {
        private readonly ICountable _countable;

        public EventProcessor(ICountable countable)
        {
            _countable = countable;
        }
        public Task ProcessAsync(TestEvent message, CancellationToken cancellationToken)
        {
            _countable.Count();
            return Task.CompletedTask;
        }
    }
}