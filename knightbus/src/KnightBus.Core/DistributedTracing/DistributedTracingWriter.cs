using System.Collections.Generic;

namespace KnightBus.Core.DistributedTracing;

// Obsolete?
public class DistributedTracingWriter : IDistributedTracingWriter
{
    private IDictionary<string, string> _messageProperties = new Dictionary<string, string>();
    
    public void Init(IDictionary<string, string> messageProperties)
    {
        _messageProperties = messageProperties;
    }

    public void Set(string traceId)
    {
        _messageProperties[DistributedTracingUtility.TraceIdKey] = traceId;
    }
}
