using System;
using System.Collections.Generic;

namespace KnightBus.Core.DistributedTracing;

public class DefaultDistributedTracingProvider : IDistributedTracingProvider
{
    private string _traceId;

    public void SetProperties(IDictionary<string, string> properties)
    {
        _traceId = properties.TryGetValue(DistributedTracingUtility.TraceIdKey, out var traceId)
            ? traceId
            : null;
    }

    public IDictionary<string, string> GetProperties()
    {
        return new Dictionary<string, string>
        {
            { DistributedTracingUtility.TraceIdKey, _traceId ?? Guid.NewGuid().ToString("N") },
        };
    }
}
