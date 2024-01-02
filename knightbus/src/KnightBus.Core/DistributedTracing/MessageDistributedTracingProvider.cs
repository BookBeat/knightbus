using System;
using System.Collections.Generic;

namespace KnightBus.Core.DistributedTracing;

public class MessageDistributedTracingProvider : IDistributedTracingProvider
{
    private IDictionary<string, string> _messageProperties = new Dictionary<string, string>();
    
    public void Init(IDictionary<string, string> messageProperties)
    {
        _messageProperties = messageProperties;
    }
    
    public string Get()
    {
        return _messageProperties.TryGetValue(DistributedTracingUtility.TraceIdKey, out var traceId) ? traceId : Guid.NewGuid().ToString("N");
    }
}
