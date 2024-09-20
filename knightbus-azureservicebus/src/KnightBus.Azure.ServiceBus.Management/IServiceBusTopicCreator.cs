using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Azure.ServiceBus.Management;

public interface IServiceBusTopicCreator
{
    Task<bool> CreateIfNotExists(string path, CancellationToken ct);
}
