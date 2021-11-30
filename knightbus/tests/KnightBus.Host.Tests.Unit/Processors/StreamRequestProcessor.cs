using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Host.Tests.Unit.Processors
{
    public class StreamRequestProcessor : IProcessStreamRequest<TestRequest, TestResponse, TestMessageSettings>
    {
        private readonly ICountable _countable;

        public StreamRequestProcessor(ICountable countable)
        {
            _countable = countable;
        }
        public async IAsyncEnumerable<TestResponse> ProcessAsync(TestRequest message, CancellationToken cancellationToken)
        {
            _countable.Count();
            await Task.Delay(1, cancellationToken);
            yield return new TestResponse();
        }
    }
}