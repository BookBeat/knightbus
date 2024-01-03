using System.Collections.Generic;

namespace KnightBus.Core.DistributedTracing;

public class MessageDistributedTracingProvider : IDistributedTracingProvider
{
    private IDictionary<string, string> _properties = new Dictionary<string, string>();

    public void SetProperties(IDictionary<string, string> properties)
    {
        _properties = properties;
    }

    public IDictionary<string, string> GetProperties()
    {
        return _properties;
    }
}
