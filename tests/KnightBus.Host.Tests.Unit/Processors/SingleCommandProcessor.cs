using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Host.Tests.Unit.Processors
{
    public class SingleCommandProcessor :
        IProcessCommand<TestCommand, TestTopicSettings>
    {
        private readonly ICountable _countable;

        public SingleCommandProcessor(ICountable countable)
        {
            _countable = countable;
        }
        
        public Task ProcessAsync(TestCommand message, CancellationToken cancellationToken)
        {
            _countable.Count();
            if (message.Throw) throw new Exception();
            return Task.CompletedTask;
        }
    }
}