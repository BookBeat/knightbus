using System.Collections.Generic;

namespace KnightBus.Core.DistributedTracing;

public interface IDistributedTracingProvider
{
    public void SetProperties(IDictionary<string, string> properties);
    public IDictionary<string, string> GetProperties();
}
