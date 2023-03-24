using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Host.Tests.Unit.ExampleProcessors
{
    public class MultipleCommandProcessor :
        IProcessCommand<TestCommandOne, TestTopicSettings>,
        IProcessCommand<TestCommandTwo, TestTopicSettings>
    {
        private readonly ICountable _countable;

        public MultipleCommandProcessor(ICountable countable)
        {
            _countable = countable;
        }
        public async Task ProcessAsync(TestCommandOne message, CancellationToken cancellationToken)
        {
            _countable.Count();
            await Task.Delay(1, cancellationToken);
            if (message.Throw) throw new TestException();
        }

        public Task ProcessAsync(TestCommandTwo message, CancellationToken cancellationToken)
        {
            _countable.Count();
            return Task.CompletedTask;
        }
    }
}
