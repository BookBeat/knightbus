using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.DistributedTracing;
using KnightBus.Messages;

namespace KnightBus.Core.PreProcessors;

public class DistributedTracingPreProcessor : IMessagePreProcessor
{
    private readonly IDistributedTracingProvider _distributedTracingProvider;

    public DistributedTracingPreProcessor(IDistributedTracingProvider distributedTracingProvider)
    {
        _distributedTracingProvider = distributedTracingProvider;
    }
    
    public Task Process<T>(T message, Action<string, string> setter, CancellationToken cancellationToken) where T : IMessage
    {
        setter(DistributedTracingUtility.TraceIdKey, _distributedTracingProvider.Get());
        return Task.CompletedTask;
    }
}
