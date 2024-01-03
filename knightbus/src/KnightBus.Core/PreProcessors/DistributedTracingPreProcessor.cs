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
        var properties = _distributedTracingProvider.GetProperties();

        IDictionary<string, object> dictionary = new Dictionary<string, object>(properties.Count);
        foreach (var property in properties)
        {
            dictionary.Add(property.Key, property.Value);
        }

        return Task.FromResult(dictionary);
    }
}
