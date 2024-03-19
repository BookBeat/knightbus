using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Host.Tests.Unit.ExampleProcessors;

public class SingleRequestProcessor : IProcessRequest<TestRequest, TestResponse, TestMessageSettings>
{
    private readonly ICountable _countable;

    public SingleRequestProcessor(ICountable countable)
    {
        _countable = countable;
    }
    public Task<TestResponse> ProcessAsync(TestRequest message, CancellationToken cancellationToken)
    {
        _countable.Count();
        return Task.FromResult(new TestResponse());
    }
}
