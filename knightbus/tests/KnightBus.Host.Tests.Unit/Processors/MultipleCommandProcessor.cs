using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Host.Tests.Unit.Processors
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
        public Task ProcessAsync(TestCommandOne message, CancellationToken cancellationToken)
        {
            _countable.Count();
            if (message.Throw) throw new Exception();
            return Task.CompletedTask;
        }

        public Task ProcessAsync(TestCommandTwo message, CancellationToken cancellationToken)
        {
            _countable.Count();
            return Task.CompletedTask;
        }
    }
}