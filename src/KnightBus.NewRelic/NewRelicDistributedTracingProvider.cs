using System.Collections.Generic;
using KnightBus.Core.DistributedTracing;
using NewRelic.Api.Agent;

namespace KnightBus.NewRelicMiddleware;

public class NewRelicDistributedTracingProvider : IDistributedTracingProvider
{
    public void SetProperties(IDictionary<string, string> properties)
    {
        NewRelic
            .Api.Agent.NewRelic.GetAgent()
            .CurrentTransaction.AcceptDistributedTraceHeaders(
                properties,
                (carrier, key) => new[] { carrier[key] },
                TransportType.Queue
            );
    }

    public IDictionary<string, string> GetProperties()
    {
        var result = new Dictionary<string, string>();
        NewRelic
            .Api.Agent.NewRelic.GetAgent()
            .CurrentTransaction.InsertDistributedTraceHeaders(
                result,
                (carrier, key, value) => carrier.Add(key, value)
            );
        return result;
    }
}
