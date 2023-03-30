using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using KnightBus.Azure.Storage;
using KnightBus.Newtonsoft;

namespace KnightBus.UI.Console.Providers.StorageBus;

public class StorageQueueManager : IQueueManager
{
    private readonly string _connectionString;
    private readonly QueueServiceClient _client;

    public StorageQueueManager(string connectionString)
    {
        _connectionString = connectionString;
        _client = new QueueServiceClient(connectionString);
    }
    public IEnumerable<QueueProperties> List(CancellationToken ct)
    {
        var queues = _client.GetQueues(cancellationToken: ct);
        foreach (var queue in queues.Where(q => !q.Name.EndsWith("-dl")))
        {
            yield return new QueueProperties(queue.Name, QueueType.Queue, this, false);
        }
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var config = new StorageBusConfiguration(_connectionString) { MessageEncoding = QueueMessageEncoding.Base64 };
        var qc = new StorageQueueClient(config, new NewtonsoftSerializer(), new BlobStorageMessageAttachmentProvider(config), path);
        var queueCount = 0;
        var dlCount = 0;

        await Task.WhenAll(
            qc.GetQueueCountAsync().ContinueWith(task => queueCount = task.Result, TaskContinuationOptions.NotOnFaulted),
            qc.GetDeadLetterCountAsync().ContinueWith(task => dlCount = task.Result, TaskContinuationOptions.NotOnFaulted)
        ).ConfigureAwait(false);


        return new QueueProperties(path, QueueType.Queue, this, false)
        {
            ActiveMessageCount = queueCount,
            DeadLetterMessageCount = dlCount
        };

    }

    public Task Delete(string path, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<int> MoveDeadLetters(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
