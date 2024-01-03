using System.Collections.Generic;

namespace KnightBus.Core.DistributedTracing;

public interface IDistributedTracingProvider
{
    void Init(IDictionary<string, string> properties);
    string Get();
}
