using System;
using System.Collections.Generic;
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
    
    public Task<IDictionary<string, object>> PreProcess<T>(T message, CancellationToken cancellationToken) where T : IMessage
    {
        IDictionary<string, object> result = new Dictionary<string, object>{ { DistributedTracingUtility.TraceIdKey, _distributedTracingProvider.Get() }};
        return Task.FromResult(result);
    }
}
